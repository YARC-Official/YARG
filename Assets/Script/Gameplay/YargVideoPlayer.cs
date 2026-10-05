#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
#define VLC_SUPPORTED
#endif

using System;
using System.Threading;
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
    private bool _vlcSeekPending;
    private double _vlcSeekTarget;
    private float _vlcSeekDeadline;
    private bool _vlcSeekAccepted;
    private long _vlcSeekFrameBaseline;

    // libVLC's watch_time timer, owned here rather than inside VLCMediaPlayer so no vendored code
    // is modified. libVLC allows one watcher per player, calls back on its own threads (which must
    // not call into the player), and asserts at player deletion that no watcher remains -- so it
    // must be removed before VLCMediaPlayer disposes the player. See StopTimeWatch.
    private LibVLCSharp.MediaPlayer _watchedPlayer;
    private VlcPlayerTeardownHook _teardownHook;
    private LibVLCSharp.MediaPlayer.WatchTimeOnUpdate _onTimeUpdate;
    private LibVLCSharp.MediaPlayer.WatchTimeOnSeek _onTimeSeek;
    private long _picturesPresented;
    private long _presentedPictureTs = long.MinValue;
    private int _vlcSeekFinished;
    private long _vlcSeekFinishedAtPicture;
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
            if (_usingVLC && _vlcPlayer != null)
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
        get => _usingVLC && _vlcPlayer != null
            ? (_vlcPlayer.MediaPlayer != null ? _vlcPlayer.MediaPlayer.Time / 1_000_000.0 : 0)
            : _unityVideoPlayer.time;
#else
        get => _unityVideoPlayer.time;
#endif
        set
        {
#if VLC_SUPPORTED
            if (_usingVLC && _vlcPlayer != null)
            {
                // Armed before the seek is issued: on_seek arrives on a libVLC thread and can beat
                // the return from SeekTo.
                BeginVlcSeekWatch(value);

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
        get => _usingVLC && _vlcPlayer != null ? _vlcPlayer.Duration / 1000.0 : _unityVideoPlayer.length;
#else
        get => _unityVideoPlayer.length;
#endif
    }

    public float playbackSpeed
    {
#if VLC_SUPPORTED
        get => _usingVLC && _vlcPlayer != null ? _vlcPlayer.MediaPlayer.Rate : _unityVideoPlayer.playbackSpeed;
#else
        get => _unityVideoPlayer.playbackSpeed;
#endif
        set
        {
#if VLC_SUPPORTED
            if (_usingVLC && _vlcPlayer != null)
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
        get => _usingVLC && _vlcPlayer != null
            ? !_vlcPlayer.MediaPlayer.IsPlaying
            : !_unityVideoPlayer.isPlaying;
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
        get => _usingVLC ? Interlocked.Read(ref _picturesPresented) : (long) _unityVideoPlayer.frame;
#else
        get => (long) _unityVideoPlayer.frame;
#endif
    }

    /// <summary>
    /// Stream time, in seconds, of the last picture libVLC presented; NaN when there is none or
    /// not using VLC. While paused this is what playback resumes from -- the paused player's
    /// reported <see cref="time"/> runs ahead of it.
    /// </summary>
    public double presentedPictureTime
    {
        get
        {
#if VLC_SUPPORTED
            long ts = Interlocked.Read(ref _presentedPictureTs);
            if (_usingVLC && ts != long.MinValue)
                return ts / 1_000_000.0;
#endif
            return double.NaN;
        }
    }

    public Camera targetCamera => _unityVideoPlayer?.targetCamera;

    // ─── Events ───

    public event Action<YargVideoPlayer> prepareCompleted;
    public event Action<YargVideoPlayer> seekCompleted;

    // ─── Methods ───

    /// <param name="mediaOptions">
    /// Per-media libVLC options (the ":option=value" form). Ignored on the Unity path, which has
    /// no equivalent. Per-media rather than instance-level so they can differ between songs
    /// without recreating LibVLC, which is process-wide and built once.
    /// </param>
    public void Prepare(params string[] mediaOptions)
    {
#if VLC_SUPPORTED
        if (_usingVLC && _vlcPlayer != null)
        {
            _vlcPreparedFired = false;
            _ = _vlcPlayer.OpenAsync(_url, mediaOptions ?? Array.Empty<string>());
            return;
        }
#endif
        // Unity VideoPlayer path. Works identically on Windows (VLC compiled out) and
        // on the VLC fallback path, so it must fully configure the player itself:
        // SwitchToVideoPlayerFallback (which also set renderMode) is VLC-only.
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
        if (_usingVLC && _vlcPlayer != null)
        {
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
        if (_usingVLC && _vlcPlayer != null)
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
        if (_usingVLC && _vlcPlayer != null)
        {
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
        UpdateSeekWatch();
    }

    // LateUpdate, not Update: VLCMediaPlayer fetches libVLC's newest picture in its own
    // Update, and the two have no defined order. Blitting in Update ran first every frame, so
    // the venue always showed the previous frame's picture.
    private void LateUpdate()
    {
        BlitToTargetTexture();
    }

    private unsafe void StartTimeWatch()
    {
        var player = _vlcPlayer != null ? _vlcPlayer.MediaPlayer : null;
        if (player == null || player == _watchedPlayer)
            return;

        StopTimeWatch();

        Interlocked.Exchange(ref _presentedPictureTs, long.MinValue);

        // Both run on libVLC threads: record and hand off, never call into the player.
        _onTimeUpdate = (point, _) =>
        {
            // INT64_MAX system date is a paused-clock update, not a presented picture.
            if (point.SystemDate != long.MaxValue)
            {
                Interlocked.Exchange(ref _presentedPictureTs, point.Time);
                Interlocked.Increment(ref _picturesPresented);
            }
        };
        _onTimeSeek = (point, _) =>
        {
            // Called with the request, then with null once seeking has finished.
            if (point.HasValue)
                return;

            Interlocked.Exchange(ref _vlcSeekFinishedAtPicture, Interlocked.Read(ref _picturesPresented));
            Interlocked.Exchange(ref _vlcSeekFinished, 1);
        };

        // 0 = every update; onPaused is unused, and the API accepts null for it.
        if (player.WatchTime(0, _onTimeUpdate, null, _onTimeSeek))
        {
            _watchedPlayer = player;

            if (_teardownHook == null)
            {
                _teardownHook = _vlcPlayer.gameObject.GetComponent<VlcPlayerTeardownHook>();
                if (_teardownHook == null)
                    _teardownHook = _vlcPlayer.gameObject.AddComponent<VlcPlayerTeardownHook>();
                _teardownHook.Disabled = StopTimeWatch;
            }
        }
        else
        {
            _onTimeUpdate = null;
            _onTimeSeek = null;
            YargLogger.LogWarning("[YargVideoPlayer] WatchTime registration failed; seek completion " +
                "falls back to its timeout.");
        }
    }

    // Must run before VLCMediaPlayer disposes the player: libVLC asserts at player deletion that no
    // watcher remains, and aborts the process. Scene unload destroys objects one at a time, so this
    // component's OnDisable may come after the VLC object is already gone; VlcPlayerTeardownHook,
    // on the VLC object itself, is what guarantees the order. Idempotent -- every path calls it.
    private void StopTimeWatch()
    {
        if (_watchedPlayer != null && _vlcPlayer != null && _vlcPlayer.MediaPlayer == _watchedPlayer)
        {
            _watchedPlayer.UnwatchTime();
        }

        _watchedPlayer = null;
        _onTimeUpdate = null;
        _onTimeSeek = null;
    }

    private void BlitToTargetTexture()
    {
        if (!_usingVLC || _vlcPlayer == null || _targetTexture == null)
        {
            return;
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
        if (_usingVLC)
            StartTimeWatch();
#endif
    }

    private void OnDisable()
    {
#if VLC_SUPPORTED
        StopTimeWatch();
#endif
    }

    private void OnDestroy()
    {
#if VLC_SUPPORTED
        // Before the Destroy below: the watcher must be gone when VLCMediaPlayer disposes the player.
        StopTimeWatch();
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
            _usingVLC = false;
            YargLogger.LogInfo("[YargVideoPlayer] VLC not available, using Unity VideoPlayer");
            SwitchToVideoPlayerFallback();
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
                _usingVLC = false;
                YargLogger.LogInfo("[YargVideoPlayer] VLC not available, using Unity VideoPlayer");
                SwitchToVideoPlayerFallback();
                return;
            }

            _usingVLC = true;
            StartTimeWatch();
            YargLogger.LogInfo("[YargVideoPlayer] VLC initialized successfully");
        }
        catch (Exception ex)
        {
            YargLogger.LogInfo("[YargVideoPlayer] VLC initialization failed, falling back to Unity VideoPlayer: " + ex.Message);
            _usingVLC = false;
            if (_vlcPlayer != null)
            {
                _vlcPlayer = null;
            }
            SwitchToVideoPlayerFallback();
        }
    }

    // Seek completion needs two signals, because libVLC gives them at different moments.
    //
    // on_seek says libVLC accepted and processed the seek, but it fires before any picture from
    // the new position exists. Completion releases WaitForSongVideo's hold on the song, so acting
    // on on_seek alone starts the song before the video can show anything.
    //
    // The second signal is a delivered frame, not proximity to the target. A seek lands on the
    // nearest decodable frame, so a target mid-GOP settles short of where it was aimed and a
    // distance test never passes -- burning the full deadline on every such seek. A frame is
    // positive evidence wherever the seek ended up: the seek flushes the decoder, so anything
    // delivered after on_seek is necessarily post-seek, and under this clock model a presented
    // picture is what re-anchors the clock.
    private const long VLC_SEEK_MIN_FRAMES = 1;
    private const float VLC_SEEK_TIMEOUT_SECONDS = 0.35f;

    private void BeginVlcSeekWatch(double target)
    {
        _vlcSeekTarget = target;
        _vlcSeekFrameBaseline = framesDelivered;
        _vlcSeekDeadline = Time.unscaledTime + VLC_SEEK_TIMEOUT_SECONDS;
        _vlcSeekPending = true;
        _vlcSeekAccepted = false;
        Interlocked.Exchange(ref _vlcSeekFinished, 0);
    }

    private void UpdateSeekWatch()
    {
        if (!_vlcSeekPending)
            return;

        // on_seek has reported the seek finished; pictures presented before that moment are
        // pre-seek, so count only those after it.
        if (!_vlcSeekAccepted && Interlocked.CompareExchange(ref _vlcSeekFinished, 0, 0) == 1)
        {
            _vlcSeekAccepted = true;
            _vlcSeekFrameBaseline = Interlocked.Read(ref _vlcSeekFinishedAtPicture);
        }

        bool landed = _vlcSeekAccepted &&
                      framesDelivered - _vlcSeekFrameBaseline >= VLC_SEEK_MIN_FRAMES;
        bool expired = Time.unscaledTime >= _vlcSeekDeadline;
        if (!landed && !expired)
            return;

        _vlcSeekPending = false;
        if (!landed)
        {
            // Must still fire: a caller may be holding the song paused waiting for it.
            YargLogger.LogFormatWarning(
                "[YargVideoPlayer] Seek to {0:F4} unconfirmed after {1:F2}s (at {2:F4}, accepted={3}, frames={4}, watchActive={5})",
                _vlcSeekTarget, VLC_SEEK_TIMEOUT_SECONDS, time, _vlcSeekAccepted,
                framesDelivered - _vlcSeekFrameBaseline,
                _watchedPlayer != null);
        }

        seekCompleted?.Invoke(this);
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

    private void SwitchToVideoPlayerFallback()
    {
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
