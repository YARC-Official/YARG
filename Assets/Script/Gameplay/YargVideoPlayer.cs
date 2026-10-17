#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN)
#define VLC_SUPPORTED
#endif

#if VLC_SUPPORTED && (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX)
#define VLC_CPU_OUTPUT
#endif

using System;
using UnityEngine;
using UnityEngine.Video;
using YARG.Core.Logging;
#if VLC_CPU_OUTPUT
using VlcPlayer = VlcCpuMediaPlayer;
#elif VLC_SUPPORTED
using VlcPlayer = LibVLCSharp.VLCMediaPlayer;
#endif

/// <summary>
/// Video player wrapper that tries VLC first (vlc-unity's VLCMediaPlayer on macOS and Windows,
/// <see cref="VlcCpuMediaPlayer"/> on Linux; see VlcPlayer),
/// falling back to Unity's built-in VideoPlayer if VLC native
/// binaries are not available. On platforms without a native plugin
/// (e.g. WSA), the VLC code path is compiled out entirely
/// (see VLC_SUPPORTED) and the class behaves as a thin Unity VideoPlayer wrapper.
/// </summary>
// Awake must run before VLCMediaPlayer's (on the "Video Player" GameObject), which builds LibVLC
// itself unless one exists. Unity wakes it at scene load even though it starts disabled.
[DefaultExecutionOrder(-100)]
public class YargVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer _unityVideoPlayer;

#if VLC_SUPPORTED
    // Only read in Awake. On Linux it is replaced by a VlcCpuMediaPlayer on the same GameObject.
    [SerializeField] private LibVLCSharp.VLCMediaPlayer _vlcPlayer;
    private VlcPlayer _vlc;
    private bool _usingVLC = false;
#endif

    private string _url = "";
    private bool _isLooping = false;
    private bool _playerEnabled = true;

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
#if VLC_SUPPORTED
            if (usingVlc)
            {
                return _vlc.OutputTexture;
            }
#endif
            return _unityVideoPlayer.targetTexture;
        }
        set
        {
#if VLC_CPU_OUTPUT
            // The picture is fitted inside it, so a larger video never needs copying at full size.
            if (_vlc != null && value != null)
                _vlc.SetMaxOutputSize(value.width, value.height);
#endif
#if VLC_SUPPORTED
            if (usingVlc && value != null)
            {
                YargLogger.LogInfo("[YargVideoPlayer] Ignoring external output texture in vlc mode");
            }
#endif
            // Always set on the built-in player too, so we can fall back to it.
            // Named here: the logger formats on its own thread, where Object.ToString throws.
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
                if (_vlc != null)
                    _vlc.enabled = value;
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
        get => usingVlc ? _vlc.Time / 1000.0 : _unityVideoPlayer.time;
#else
        get => _unityVideoPlayer.time;
#endif
        set
        {
            YargLogger.LogFormatDebug("YargVideoPlayer::SetTime {0}", value);
#if VLC_SUPPORTED
            if (usingVlc)
            {
                _vlc.SetTime((long)(value * 1000));
                // LibVLC's SetTime (SeekTo) is synchronous, so the seek has already
                // completed. VLC exposes no async seek-completion callback, so fire
                // seekCompleted here so BackgroundManager.OnVideoSeeked can resume
                // playback, reset _videoSeeking and (optionally) unpause the game.
                seekCompleted?.Invoke(this);
                return;
            }
#endif
            _unityVideoPlayer.time = value;
        }
    }

    public double length
    {
#if VLC_SUPPORTED
        get => usingVlc ? _vlc.Duration / 1000.0 : _unityVideoPlayer.length;
#else
        get => _unityVideoPlayer.length;
#endif
    }

    public float playbackSpeed
    {
#if VLC_SUPPORTED
        get => usingVlc ? _vlc.MediaPlayer.Rate : _unityVideoPlayer.playbackSpeed;
#else
        get => _unityVideoPlayer.playbackSpeed;
#endif
        set
        {
#if VLC_SUPPORTED
            if (usingVlc)
                _vlc.MediaPlayer.SetRate(value);
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
        get => usingVlc
            ? !_vlc.MediaPlayer.IsPlaying
            : !_unityVideoPlayer.isPlaying;
#else
        get => !_unityVideoPlayer.isPlaying;
#endif
    }

    public Camera targetCamera => _unityVideoPlayer?.targetCamera;

    /// <summary>Which backend actually took the video. A VLC init failure falls back to Unity's
    /// player silently, and the two do not behave alike.</summary>
    public bool usingVlc =>
#if VLC_SUPPORTED
        _usingVLC && _vlc != null;
#else
        false;
#endif

    // ─── Events ───

    public event Action<YargVideoPlayer> prepareCompleted;
    public event Action<YargVideoPlayer> seekCompleted;

    // ─── Methods ───

    public void Prepare()
    {
#if VLC_SUPPORTED
        if (usingVlc)
        {
            PrepareVlc();
            return;
        }
#endif
        PrepareUnity();
    }

#if VLC_SUPPORTED
    // OpenAsync throws inside its task when libvlc lacks a function LibVLCSharp needs (a libvlc
    // from another API era), so observe it and fall back to Unity's player instead of never starting.
    private async void PrepareVlc()
    {
        try
        {
            await _vlc.OpenAsync(_url);
        }
        catch (Exception ex)
        {
            FallBackToUnity("VLC failed to open the video: " + ex);
            PrepareUnity();
        }
    }
#endif

    private void PrepareUnity()
    {
        // Unity VideoPlayer path. Works identically on Windows (VLC compiled out) and
        // on the VLC fallback path, so it must fully configure the player itself.
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
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
            _vlc.Play();
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
            _vlc.Pause();
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
            _vlc.Stop();
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
        if (_vlcPlayer != null && _vlcPlayer.didAwake)
            YargLogger.LogError("[YargVideoPlayer] VLCMediaPlayer woke first; its own libVLC init may have poisoned VLC for this session");

        if (_vlcPlayer == null)
            return;

        if (!VlcLibraryLoader.EnsureLoaded(_vlcPlayer))
        {
            DestroyImmediate(_vlcPlayer);
            _vlcPlayer = null;
            return;
        }

#if VLC_CPU_OUTPUT
        // Removed before it wakes, so it never creates a player through vlc-unity's Linux output.
        var playerObject = _vlcPlayer.gameObject;
        DestroyImmediate(_vlcPlayer);
        _vlcPlayer = null;
        _vlc = playerObject.AddComponent<VlcCpuMediaPlayer>();
#else
        _vlc = _vlcPlayer;
#endif
#endif
    }

    private void Start()
    {
#if VLC_SUPPORTED
        TryInitializeVLC();
#endif
    }

    private void OnDestroy()
    {
#if VLC_SUPPORTED
        if (_vlc != null)
        {
            try
            {
                _vlc.Stop();
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning("[YargVideoPlayer] Error stopping VLC player: " + ex.Message);
            }
            Destroy(_vlc.gameObject);
        }

        // Outside the check: scene unload can destroy the VLC player's GameObject before this one,
        // making _vlc null here after it already released its renderer.
        VlcLibraryLoader.ScheduleRendererCleanup();
#endif
    }

    // ─── VLC initialization ───

#if VLC_SUPPORTED
    private void TryInitializeVLC()
    {
        if (_vlc == null)
        {
            FallBackToUnity("VLC not available");
            return;
        }

        try
        {
            _vlc.enabled = true;
#if !VLC_CPU_OUTPUT
            _vlc.playOnAwake = false;
            _vlc.flipTextureX = true;
            _vlc.flipTextureY = true;
            _vlc.logPlayerActivity = false;
#endif
            _vlc.OnTextureResized += OnVLCTextureResized;

            if (!_vlc.enabled || !_vlc.didAwake || _vlc.MediaPlayer == null)
            {
                throw new InvalidOperationException("VLC player failed initialization");
            }

            _usingVLC = true;
            YargLogger.LogInfo("[YargVideoPlayer] VLC initialized successfully");
        }
        catch (Exception ex)
        {
            if (_vlc != null)
            {
                // It was already enabled above, so it would otherwise keep updating.
                Destroy(_vlc);
                _vlc = null;
            }
            FallBackToUnity("VLC initialization failed: " + ex.Message);
        }
    }

    private void OnVLCTextureResized(RenderTexture texture)
    {
        // Both players raise this with null when they tear the textures down. Throwing here aborts
        // the player's OnDestroy before it releases the native player, which then keeps its
        // renderer and video-output thread alive for the rest of the process.
        if (texture == null || texture.height == 0)
        {
            return;
        }

        // OpenAsync() starts playing, so stop before handing over to the caller.
        Stop();

        prepareCompleted?.Invoke(this);
    }

    private void FallBackToUnity(string reason)
    {
        YargLogger.LogInfo($"[YargVideoPlayer] {reason}; using Unity VideoPlayer");
        _usingVLC = false;
        _unityVideoPlayer.enabled = true;
        // PrepareUnity configures the player, so the Unity path stays identical whether we got
        // here via fallback or via a VLC-less build.
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
