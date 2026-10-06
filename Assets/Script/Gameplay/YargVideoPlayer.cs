#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
#define VLC_SUPPORTED
#endif

using System;
using UnityEngine;
using UnityEngine.Video;
using YARG.Core.Logging;

/// <summary>
/// Video player wrapper that tries VLC (via vlc-unity) first,
/// falling back to Unity's built-in VideoPlayer if VLC native
/// binaries are not available. On platforms without a native plugin
/// (e.g. WSA), the VLC code path is compiled out entirely
/// (see VLC_SUPPORTED) and the class behaves as a thin Unity VideoPlayer wrapper.
/// </summary>
// Before VLCMediaPlayer (on the same GameObject), whose Awake builds LibVLC itself unless one exists.
[DefaultExecutionOrder(-100)]
public class YargVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer _unityVideoPlayer;

#if VLC_SUPPORTED
    [SerializeField] private LibVLCSharp.VLCMediaPlayer _vlcPlayer;
    private bool _usingVLC = false;
    // The 180-degree flip happens in BlitContained rather than in VLCMediaPlayer; see TryInitializeVLC.
    private bool _flipInBlit;
    // Latches prepareCompleted to one fire per Prepare() -- see OnVLCTextureResized.
    private bool _vlcPreparedFired = false;
    private VlcTimeWatch _timeWatch;
    // Distinguishes our Stop() from libVLC stopping at the end of the media; see Update.
    private bool _vlcStopRequested;
    // Pictures presented when a loop restart was issued; -1 when none is pending. See BlitToTargetTexture.
    private long _loopRestartPictures = -1;

    // Per-media rather than on LibVLC, which is process-wide and built once.
    private static readonly string[] VLC_MEDIA_OPTIONS =
    {
        // Load-bearing for sync. libavcodec's frame threading delays decoder output by
        // thread_count - 1 frames, and at 24-30fps that delay is the whole offset the video would
        // otherwise sit behind the song at. No seek or resume timing can shift it.
        ":avcodec-threads=1",
        ":low-delay",
        // libVLC maps its first picture after a seek to "now + input caching" in wall time while
        // the stream advances at the playback rate, so at rate r the video settles
        // caching x (r - 1) off (-0.21s at 120% with the default 1000ms). Local files need little.
        ":file-caching=100",
        // Insurance: background videos are silent, and a badly muxed one must not reach the
        // speakers. Not a sync lever: with no audio track, VLC's master clock is the video.
        ":no-audio",
        // Never ":start-time=". It decouples the player's reported time from the picture shown, so
        // the video runs seconds out while every time-based check still reads correct.
    };
#endif

    private string _url = "";
    private bool _isLooping = false;
    private bool _playerEnabled = true;

    // The externally-owned texture every consumer reads back through targetTexture. Both
    // backends fit themselves into it rather than rendering at their own native size --
    // VLC via the blit in LateUpdate, the built-in player via VideoAspectRatio.FitInside.
    private RenderTexture _targetTexture;

    // ─── Properties matching VideoPlayer API ───

    public string url
    {
        get => _url;
        set => _url = value;
    }

    public RenderTexture targetTexture
    {
        get
        {
            if (_targetTexture != null)
            {
                return _targetTexture;
            }
#if VLC_SUPPORTED
            if (usingVlc)
            {
                return _vlcPlayer.OutputTexture;
            }
#endif
            return _unityVideoPlayer.targetTexture;
        }
        set
        {
            _targetTexture = value;
            // Always set on the built-in player too, so we can fall back to it.
            // Log the name, not the RenderTexture -- YargLogger has no formatter for it and throws.
            YargLogger.LogFormatDebug("[YargVideoPlayer/UnityPlayer] targetTexture set to {0}", value != null ? value.name : "null");
            _unityVideoPlayer.targetTexture = value;
        }
    }

    /// <summary>
    /// Controls whether the underlying player (VLC or VideoPlayer) is enabled.
    /// Does NOT control this MonoBehaviour's Update loop.
    /// </summary>
    public bool playerEnabled
    {
        get => _playerEnabled;
        set
        {
            _playerEnabled = value;
#if VLC_SUPPORTED
            if (_usingVLC)
            {
                if (_vlcPlayer != null)
                    _vlcPlayer.enabled = value;
            }
            else
#endif
            {
                if (_unityVideoPlayer != null)
                    _unityVideoPlayer.enabled = value;
            }
        }
    }

    public double time
    {
#if VLC_SUPPORTED
        // MediaPlayer.Time is in microseconds; VLCMediaPlayer.Time truncates it to milliseconds.
        get => usingVlc
            ? (_vlcPlayer.MediaPlayer != null ? _vlcPlayer.MediaPlayer.Time / 1_000_000.0 : 0)
            : _unityVideoPlayer.time;
#else
        get => _unityVideoPlayer.time;
#endif
        set
        {
#if VLC_SUPPORTED
            if (usingVlc)
            {
                _timeWatch.BeginSeek(value);

                // fast: false seeks to the requested timestamp rather than the preceding keyframe,
                // which can be seconds out on a long-GOP video. SeekTo returns false when there's
                // no input to seek within (e.g. after Stop()); it returns true for a seek issued
                // while paused, which is dropped all the same.
                if (!_vlcPlayer.MediaPlayer.SeekTo(TimeSpan.FromSeconds(value), fast: false))
                    YargLogger.LogFormatWarning("[YargVideoPlayer] Seek to {0:F4} failed (state={1})",
                        value, _vlcPlayer.CurrentState);
                return;
            }
#endif
            _unityVideoPlayer.time = value;
        }
    }

    public double length
    {
#if VLC_SUPPORTED
        get => usingVlc ? _vlcPlayer.Duration / 1000.0 : _unityVideoPlayer.length;
#else
        get => _unityVideoPlayer.length;
#endif
    }

    public float playbackSpeed
    {
#if VLC_SUPPORTED
        get => usingVlc ? _vlcPlayer.MediaPlayer.Rate : _unityVideoPlayer.playbackSpeed;
#else
        get => _unityVideoPlayer.playbackSpeed;
#endif
        set
        {
#if VLC_SUPPORTED
            if (usingVlc)
                _vlcPlayer.MediaPlayer.SetRate(value);
            else if (_unityVideoPlayer != null)
                _unityVideoPlayer.playbackSpeed = value;
#else
            if (_unityVideoPlayer != null)
                _unityVideoPlayer.playbackSpeed = value;
#endif
        }
    }

    public bool isLooping
    {
#if VLC_SUPPORTED
        get => _usingVLC ? _isLooping : _unityVideoPlayer.isLooping;
        set
        {
            _isLooping = value;
            if (!_usingVLC)
                _unityVideoPlayer.isLooping = value;
        }
#else
        get => _unityVideoPlayer.isLooping;
        set => _unityVideoPlayer.isLooping = value;
#endif
    }

    public bool isPaused
    {
#if VLC_SUPPORTED
        get => usingVlc ? !_vlcPlayer.MediaPlayer.IsPlaying : !_unityVideoPlayer.isPlaying;
#else
        get => !_unityVideoPlayer.isPlaying;
#endif
    }

    /// <summary>Which backend actually took the video. A VLC init failure falls back to Unity's
    /// player silently, and the two do not behave alike.</summary>
    public bool usingVlc =>
#if VLC_SUPPORTED
        _usingVLC && _vlcPlayer != null;
#else
        false;
#endif

    /// <summary>
    /// Pictures presented so far: on VLC, counted from libVLC's own per-picture timing updates;
    /// on Unity, its frame index. The direct evidence that the decoder is producing pictures --
    /// <see cref="time"/> alone can report a position nothing has been decoded for.
    /// </summary>
    public long framesDelivered
    {
#if VLC_SUPPORTED
        get => usingVlc ? _timeWatch.PicturesPresented : (long) _unityVideoPlayer.frame;
#else
        get => (long) _unityVideoPlayer.frame;
#endif
    }

    /// <summary>
    /// Stream time, in seconds, of the last picture libVLC presented; NaN when there is none or
    /// not using VLC. While paused this is what playback resumes from -- the paused player's
    /// reported <see cref="time"/> runs ahead of it.
    /// </summary>
    public double presentedPictureTime =>
#if VLC_SUPPORTED
        usingVlc ? _timeWatch.PresentedPictureTime : double.NaN;
#else
        double.NaN;
#endif

    /// <summary>
    /// Where a paused player will resume from: <see cref="presentedPictureTime"/>, or
    /// <see cref="time"/> when there is no presented picture to go by.
    /// </summary>
    public double resumePictureTime
    {
        get
        {
            double picture = presentedPictureTime;
            return double.IsNaN(picture) ? time : picture;
        }
    }

    /// <summary>
    /// Wall-clock seconds to aim the video ahead of the song. A VLC picture reaches the screen a
    /// fixed interval after libVLC presents it, so a video aimed exactly at the song shows late;
    /// Unity's player has no such lag. About two 24fps frames, the same on every platform
    /// (verified by eye on macOS and Linux). Not derived from the frame rate: libVLC reports
    /// 0 fps for fragmented MP4s, which are common.
    /// </summary>
    public double pipelineLeadSeconds => usingVlc ? 0.08 : 0.0;

    public Camera targetCamera => _unityVideoPlayer?.targetCamera;

    // ─── Events ───

    public event Action<YargVideoPlayer> prepareCompleted;
    public event Action<YargVideoPlayer> seekCompleted;

    // ─── Methods ───

    public void Prepare()
    {
#if VLC_SUPPORTED
        if (usingVlc)
        {
            _vlcPreparedFired = false;
            _vlcStopRequested = false;
            _ = _vlcPlayer.OpenAsync(_url, VLC_MEDIA_OPTIONS);
            return;
        }
#endif
        // Unity VideoPlayer path. Works identically on Windows (VLC compiled out) and
        // on the VLC fallback path, so it must fully configure the player itself:
        // FallBackToUnity (which also sets renderMode) is VLC-only.
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        // Letterbox rather than stretch into targetTexture, matching the VLC path's blit. The
        // built-in player does this at decode time, so it needs no blit.
        _unityVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        // (Re)wire native events idempotently so per-song Prepare() calls on a persisted
        // player don't stack seekCompleted handlers. OnUnityVideoPrepared self-unregisters
        // after the first fire; seekCompleted stays attached for every seek.
        _unityVideoPlayer.prepareCompleted -= OnUnityVideoPrepared;
        _unityVideoPlayer.seekCompleted -= OnUnitySeekCompleted;
        _unityVideoPlayer.errorReceived -= OnUnityVideoError;
        _unityVideoPlayer.prepareCompleted += OnUnityVideoPrepared;
        _unityVideoPlayer.seekCompleted += OnUnitySeekCompleted;
        _unityVideoPlayer.errorReceived += OnUnityVideoError;
        _unityVideoPlayer.Prepare();
    }

    public void Play()
    {
#if VLC_SUPPORTED
        if (usingVlc)
        {
            _vlcStopRequested = false;
            _vlcPlayer.Play();
            return;
        }
#endif
        if (_unityVideoPlayer != null)
            _unityVideoPlayer.Play();
    }

    public void Pause()
    {
#if VLC_SUPPORTED
        if (usingVlc)
        {
            // SetPause, never Pause(): VLCMediaPlayer.Pause() is libvlc_media_player_pause, which
            // toggles -- on a player that hasn't reached Playing yet it starts playback instead.
            _vlcPlayer.MediaPlayer.SetPause(true);
            return;
        }
#endif
        if (_unityVideoPlayer != null)
            _unityVideoPlayer.Pause();
    }

    public void Stop()
    {
#if VLC_SUPPORTED
        if (usingVlc)
        {
            _vlcStopRequested = true;
            _vlcPlayer.Stop();
            return;
        }
#endif
        if (_unityVideoPlayer != null)
            _unityVideoPlayer.Stop();
    }

    // ─── Unity lifecycle ───

    private void Awake()
    {
#if VLC_SUPPORTED
        // Without a usable libVLC, VLCMediaPlayer's own Awake would run Core.Initialize against a
        // path known not to work, poisoning VLC for the rest of the process. Remove it before it
        // wakes, and use Unity's player instead.
        if (_vlcPlayer != null && !VlcLibraryLoader.EnsureLoaded(_vlcPlayer))
        {
            DestroyImmediate(_vlcPlayer);
            _vlcPlayer = null;
        }
#endif
    }

    private void Start()
    {
#if VLC_SUPPORTED
        TryInitializeVLC();
#endif
    }

#if VLC_SUPPORTED
    private void Update()
    {
        if (!usingVlc)
            return;

        var status = _timeWatch.PollSeek();
        if (status == VlcTimeWatch.SeekStatus.TimedOut)
        {
            YargLogger.LogFormatWarning(
                "[YargVideoPlayer] Seek to {0:F4} unconfirmed after {1:F2}s (at {2:F4}, accepted={3}, frames={4}, watchActive={5})",
                _timeWatch.SeekTarget, VlcTimeWatch.SEEK_TIMEOUT_SECONDS, time, _timeWatch.SeekAccepted,
                _timeWatch.PicturesSinceSeek, _timeWatch.Active);
        }

        if (status is VlcTimeWatch.SeekStatus.Landed or VlcTimeWatch.SeekStatus.TimedOut)
            seekCompleted?.Invoke(this);

        // libVLC 4 stops at the end of the media, and Play on a stopped player restarts it from
        // the start. Only a stop we didn't request loops.
        if (LoopsOnStop && _vlcPlayer.CurrentState == LibVLCSharp.VLCState.Stopped)
        {
            _loopRestartPictures = _timeWatch.PicturesPresented;
            _vlcPlayer.Play();
        }
    }

    // LateUpdate, not Update: VLCMediaPlayer fetches libVLC's newest picture in its own Update,
    // and the two have no defined order, so blitting in Update shows the previous frame's picture.
    private void LateUpdate()
    {
        BlitToTargetTexture();
    }

    // A stop now would be libVLC reaching the end of a looping video, not our Stop().
    private bool LoopsOnStop => _isLooping && !_vlcStopRequested;

    private void BlitToTargetTexture()
    {
        if (!usingVlc || _targetTexture == null)
        {
            return;
        }

        // Across a loop restart VLC's output goes black until the reopened media presents its first
        // picture, so hold the last frame blitted until then.
        if (LoopsOnStop && _vlcPlayer.CurrentState is LibVLCSharp.VLCState.Stopping or LibVLCSharp.VLCState.Stopped)
            return;

        if (_loopRestartPictures >= 0)
        {
            if (_timeWatch.PicturesPresented <= _loopRestartPictures)
                return;
            _loopRestartPictures = -1;
        }

        // Re-read each frame: VLC replaces the instance on a resize (ResizeOutputTextures
        // destroys the old one) and drops it on Stop(), so it must never be cached.
        var source = _vlcPlayer.OutputTexture;
        if (source == null || source.width == 0 || source.height == 0)
        {
            return;
        }

        BlitContained(source, _targetTexture, _flipInBlit);
    }

    // Centers source inside dest at source's own aspect ratio, filling the remainder with
    // black bars. VLC decodes at the video's native resolution, and every consumer of
    // targetTexture -- the venue RawImage, the yarground screen material -- maps the whole
    // texture onto a fixed rect, so without this the video is stretched to that rect.
    // flip rotates the picture 180 degrees, the flipTextureX/Y the VLC player would otherwise apply.
    private static void BlitContained(Texture source, RenderTexture dest, bool flip)
    {
        float sourceAspect = (float) source.width / source.height;

        float width = dest.width;
        float height = dest.height;
        if (sourceAspect > (float) dest.width / dest.height)
        {
            height = Mathf.Round(dest.width / sourceAspect);
        }
        else
        {
            width = Mathf.Round(dest.height * sourceAspect);
        }

        // Whole pixels, or the edges shimmer as the fitted rect lands on half-pixels.
        var fitted = new Rect(Mathf.Round((dest.width - width) / 2f),
            Mathf.Round((dest.height - height) / 2f), width, height);

        var previous = RenderTexture.active;
        Graphics.SetRenderTarget(dest);
        // The bars themselves. Without this they keep whatever the texture last held.
        GL.Clear(false, true, Color.black);
        GL.PushMatrix();
        // y-down (bottom > top), which is what keeps this stage orientation-neutral:
        // Graphics.DrawTexture maps v=0 to the rect's top edge, so a y-up matrix flips the
        // image. The pipeline's flip convention lives elsewhere: _flipInBlit (or _vlcPlayer's
        // flipTextureX/Y) and the yarground image blit's (1, -1).
        GL.LoadPixelMatrix(0, dest.width, dest.height, 0);
        if (flip)
            Graphics.DrawTexture(fitted, source, new Rect(1f, 1f, -1f, -1f), 0, 0, 0, 0);
        else
            Graphics.DrawTexture(fitted, source);
        GL.PopMatrix();
        RenderTexture.active = previous;
    }
#endif

    private void OnEnable()
    {
#if VLC_SUPPORTED
        if (usingVlc)
            _timeWatch.Start();
#endif
    }

    private void OnDisable()
    {
#if VLC_SUPPORTED
        _timeWatch?.Stop();
#endif
    }

    private void OnDestroy()
    {
#if VLC_SUPPORTED
        // Before the Destroy below: the watcher must be gone when VLCMediaPlayer disposes the player.
        _timeWatch?.Stop();
        if (_vlcPlayer != null)
        {
            try
            {
                _vlcPlayer.Stop();
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning("[YargVideoPlayer] Error stopping VLC player: " + ex.Message);
            }
            Destroy(_vlcPlayer.gameObject);
        }
#endif
    }

    // ─── VLC initialization ───

#if VLC_SUPPORTED
    private void TryInitializeVLC()
    {
        if (_vlcPlayer == null)
        {
            FallBackToUnity("VLC not available");
            return;
        }

        try
        {
            _vlcPlayer.enabled = true;
            _vlcPlayer.playOnAwake = false;
            // On Linux Vulkan, any flip makes vlc-unity copy each frame into a Texture2D and
            // Graphics.Blit it into a second texture. Without one, the plugin writes straight into
            // OutputTexture, and the flip rides along in our own blit.
            _flipInBlit = (Application.platform is RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor) &&
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
            _vlcPlayer.flipTextureX = !_flipInBlit;
            _vlcPlayer.flipTextureY = !_flipInBlit;
            YargLogger.LogInfo(_flipInBlit
                ? "[YargVideoPlayer] VLC output: direct Vulkan, flip in blit"
                : "[YargVideoPlayer] VLC output: vlc-unity flip");
            _vlcPlayer.logPlayerActivity = false;
            _vlcPlayer.OnTextureResized += OnVLCTextureResized;

            if (!_vlcPlayer.enabled || !_vlcPlayer.didAwake || _vlcPlayer.MediaPlayer == null)
            {
                throw new InvalidOperationException("VLC player failed initialization");
            }

            if (LibVLCSharp.VLCMediaPlayer.LibVLC == null)
            {
                FallBackToUnity("VLC not available");
                return;
            }

            _usingVLC = true;
            _timeWatch = new VlcTimeWatch(_vlcPlayer);
            _timeWatch.Start();
            YargLogger.LogInfo("[YargVideoPlayer] VLC initialized successfully");
        }
        catch (Exception ex)
        {
            _vlcPlayer = null;
            FallBackToUnity("VLC initialization failed: " + ex.Message);
        }
    }

    private void OnVLCTextureResized(RenderTexture texture)
    {
        // vlc-unity raises this with null when it tears the textures down. Throwing here aborts
        // VLCMediaPlayer.OnDestroy before it releases the native player, which then keeps its
        // renderer and video-output thread alive for the rest of the process.
        if (texture == null || texture.height == 0)
        {
            return;
        }

        // Fire once per Prepare(), matching the Unity path's self-unregistering
        // OnUnityVideoPrepared. OnTextureResized is raised on every decoded-size change, not
        // just the first, so a mid-song resolution change re-enters the caller's prepare
        // handler and re-runs the whole initial setup under the player.
        if (_vlcPreparedFired)
        {
            return;
        }

        _vlcPreparedFired = true;

        // Deliberately left playing. OpenAsync() starts playback, and the caller's prepare
        // handler seeks to the video's start position -- a seek issued while stopped or paused
        // is silently dropped, and Stop() releases libVLC's input entirely with nothing here
        // to reopen it. The handler pauses once its seek is away.
        prepareCompleted?.Invoke(this);
    }

    private void FallBackToUnity(string reason)
    {
        YargLogger.LogInfo($"[YargVideoPlayer] {reason}; using Unity VideoPlayer");
        _usingVLC = false;
        _unityVideoPlayer.enabled = true;
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _unityVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        // prepareCompleted/seekCompleted are wired in Prepare() instead, so the Unity
        // path stays identical whether we got here via fallback or via a VLC-less build.
    }
#endif

    private void OnUnityVideoPrepared(VideoPlayer vp)
    {
        _unityVideoPlayer.prepareCompleted -= OnUnityVideoPrepared;
        prepareCompleted?.Invoke(this);
    }

    private void OnUnitySeekCompleted(VideoPlayer vp)
    {
        seekCompleted?.Invoke(this);
    }

    private void OnUnityVideoError(VideoPlayer vp, string message)
    {
        YargLogger.LogError(message);
    }
}
