#if (UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX)
#define VLC_SUPPORTED
#endif

using System;
using UnityEngine;
using UnityEngine.Video;
using YARG.Core.Logging;

/// <summary>
/// Video player wrapper that tries VLC (via vlc-unity) first,
/// falling back to Unity's built-in VideoPlayer if VLC native
/// binaries are not available. On Windows / WSA, the VLC code
/// path is compiled out entirely (see VLC_SUPPORTED) and the
/// class behaves as a thin Unity VideoPlayer wrapper.
/// </summary>
public class YargVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer _unityVideoPlayer;

#if VLC_SUPPORTED
    [SerializeField] private LibVLCSharp.VLCMediaPlayer _vlcPlayer;
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
            if (_usingVLC && _vlcPlayer != null)
            {
                return _vlcPlayer.OutputTexture;
            }
#endif
            return _unityVideoPlayer.targetTexture;
        }
        set
        {
#if VLC_SUPPORTED
            if (_usingVLC && _vlcPlayer != null && value != null)
            {
                YargLogger.LogInfo("[YargVideoPlayer] Ignoring external output texture in vlc mode");
            }
#endif
            // Always set on the built-in player too, so we can fall back to it.
            YargLogger.LogFormatDebug("[YargVideoPlayer/UnityPlayer] targetTexture set to {0}", value);
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
        get => _usingVLC && _vlcPlayer != null ? _vlcPlayer.Time / 1000.0 : _unityVideoPlayer.time;
#else
        get => _unityVideoPlayer.time;
#endif
        set
        {
            YargLogger.LogFormatDebug("YargVideoPlayer::SetTime {0}", value);
#if VLC_SUPPORTED
            if (_usingVLC && _vlcPlayer != null)
            {
                _vlcPlayer.SetTime((long)(value * 1000));
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

    public Camera targetCamera => _unityVideoPlayer?.targetCamera;

    // ─── Events ───

    public event Action<YargVideoPlayer> prepareCompleted;
    public event Action<YargVideoPlayer> seekCompleted;

    // ─── Methods ───

    public void Prepare()
    {
#if VLC_SUPPORTED
        if (_usingVLC && _vlcPlayer != null)
        {
            _ = _vlcPlayer.OpenAsync(_url);
            return;
        }
#endif
        // Unity VideoPlayer path. Works identically on Windows (VLC compiled out) and
        // on the VLC fallback path, so it must fully configure the player itself:
        // SwitchToVideoPlayerFallback (which also set renderMode) is VLC-only.
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        // (Re)wire native events idempotently so per-song Prepare() calls on a persisted
        // player don't stack seekCompleted handlers. OnUnityVideoPrepared self-unregisters
        // after the first fire; seekCompleted stays attached for every seek.
        _unityVideoPlayer.prepareCompleted -= OnUnityVideoPrepared;
        _unityVideoPlayer.seekCompleted -= OnUnitySeekCompleted;
        _unityVideoPlayer.prepareCompleted += OnUnityVideoPrepared;
        _unityVideoPlayer.seekCompleted += OnUnitySeekCompleted;
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
            _vlcPlayer.Pause();
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

    private void Start()
    {
#if VLC_SUPPORTED
        TryInitializeVLC();
#endif
    }

    private void OnDestroy()
    {
#if VLC_SUPPORTED
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
        try
        {
            _vlcPlayer.enabled = true;
            _vlcPlayer.playOnAwake = false;
            _vlcPlayer.flipTextureX = true;
            _vlcPlayer.flipTextureY = true;
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
            YargLogger.LogInfo("[YargVideoPlayer] VLC initialized successfully");
        }
        catch (Exception ex)
        {
            YargLogger.LogWarning("[YargVideoPlayer] VLC initialization failed, falling back to Unity VideoPlayer: " + ex.Message);
            _usingVLC = false;
            if (_vlcPlayer != null)
            {
                _vlcPlayer = null;
            }
            SwitchToVideoPlayerFallback();
        }
    }

    private void OnVLCTextureResized(RenderTexture texture)
    {
        if (texture.height == 0)
        {
            return;
        }


        prepareCompleted?.Invoke(this);
    }

    private void SwitchToVideoPlayerFallback()
    {
        _usingVLC = false;
        _unityVideoPlayer.enabled = true;
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
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
}
