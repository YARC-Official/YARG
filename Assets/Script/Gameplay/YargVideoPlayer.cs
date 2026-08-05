using System;
using UnityEngine;
using UnityEngine.Video;
using YARG.Core.Logging;

/// <summary>
/// Video player wrapper that tries VLC (via vlc-unity) first,
/// falling back to Unity's built-in VideoPlayer if VLC native
/// binaries are not available.
/// </summary>
public class YargVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer _unityVideoPlayer;
    [SerializeField] private LibVLCSharp.VLCMediaPlayer _vlcPlayer;
    private bool _usingVLC = false;
    private bool _vlcPrepared = false;
    private bool _prepareCalled = false;
    private float _prepareStartTime = 0f;
    private const float VLC_PREPARE_TIMEOUT = 5f;

    private string _url = "";
    private VideoRenderMode _renderMode;
    private double _time;
    private float _playbackSpeed = 1.0f;
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
            if (_usingVLC && _vlcPlayer != null)
            {
                return _vlcPlayer.OutputTexture;
            }
            else 
            {
                return _unityVideoPlayer.targetTexture;
            }
        }
        set
        {
            if (_usingVLC && _vlcPlayer != null && value != null)
            {
                Debug.Log($"[YargVideoPlayer] Ignoring external output texture in vlc mode");
            }
            // Always setting it on built in player so we can fallback to it
            {
                Debug.Log($"[YargVideoPlayer/UnityPlayer] targetTexture set to {value}");
                _unityVideoPlayer.targetTexture = value;
            }
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
            if (_usingVLC)
            {
                if (_vlcPlayer != null)
                    _vlcPlayer.enabled = value;
            }
            else
            {
                if (_unityVideoPlayer != null)
                    _unityVideoPlayer.enabled = value;
            }
        }
    }

    public double time
    {
        get => _usingVLC && _vlcPlayer != null ? _vlcPlayer.Time / 1000.0 : _unityVideoPlayer.time;
        set
        {
            YargLogger.LogFormatDebug("YargVideoPlayer::SetTime {0}", value);
            if (_usingVLC && _vlcPlayer != null)
            {
                _vlcPlayer.SetTime((long)(value * 1000));
                // LibVLC's SetTime (SeekTo) is synchronous, so the seek has already
                // completed. VLC exposes no async seek-completion callback, so fire
                // seekCompleted here so BackgroundManager.OnVideoSeeked can resume
                // playback, reset _videoSeeking and (optionally) unpause the game.
                seekCompleted?.Invoke(this);
            }
            else
                _unityVideoPlayer.time = value;
        }
    }

    public double length => _usingVLC && _vlcPlayer != null
        ? _vlcPlayer.Duration / 1000.0
        : _unityVideoPlayer.length;

    public float playbackSpeed
    {
        get => _usingVLC && _vlcPlayer != null ? _vlcPlayer.MediaPlayer.Rate : _unityVideoPlayer.playbackSpeed;
        set
        {
            _playbackSpeed = value;
            if (_usingVLC && _vlcPlayer != null)
                _vlcPlayer.MediaPlayer.SetRate(value);
            else if (_unityVideoPlayer != null)
                _unityVideoPlayer.playbackSpeed = value;
        }
    }

    public bool isLooping
    {
        get => _usingVLC ? _isLooping : _unityVideoPlayer.isLooping;
        set
        {
            _isLooping = value;
            if (!_usingVLC)
                _unityVideoPlayer.isLooping = value;
        }
    }

    public Camera targetCamera => _unityVideoPlayer?.targetCamera;

    // ─── Events ───

    public event Action<YargVideoPlayer> prepareCompleted;
    public event Action<YargVideoPlayer> seekCompleted;

    // ─── Methods ───

    public void Prepare()
    {
        _prepareCalled = true;
        _prepareStartTime = Time.time;
        if (_usingVLC && _vlcPlayer != null)
        {
            _vlcPrepared = false;
            _ = _vlcPlayer.OpenAsync(_url);
        }
        else
        {
            _unityVideoPlayer.url = _url;
            _unityVideoPlayer.Prepare();
        }
    }

    public void Play()
    {
        if (_usingVLC && _vlcPlayer != null)
            _vlcPlayer.Play();
        else if (_unityVideoPlayer != null)
            _unityVideoPlayer.Play();
    }

    public void Pause()
    {
        if (_usingVLC && _vlcPlayer != null)
            _vlcPlayer.Pause();
        else if (_unityVideoPlayer != null)
            _unityVideoPlayer.Pause();
    }

    public void Stop()
    {
        if (_usingVLC && _vlcPlayer != null)
            _vlcPlayer.Stop();
        else if (_unityVideoPlayer != null)
            _unityVideoPlayer.Stop();
    }

    // ─── Unity lifecycle ───

    private void Start()
    {
        TryInitializeVLC();
    }

    private void OnDestroy()
    {
        if (_vlcPlayer != null)
        {
            try
            {
                _vlcPlayer.Stop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[YargVideoPlayer] Error stopping VLC player: " + ex.Message);
            }
            Destroy(_vlcPlayer.gameObject);
        }
    }

    // ─── VLC initialization ───

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
                Debug.Log("[YargVideoPlayer] VLC not available, using Unity VideoPlayer");
                return;
            }

            _usingVLC = true;
            Debug.Log("[YargVideoPlayer] VLC initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[YargVideoPlayer] VLC initialization failed, falling back to Unity VideoPlayer: " + ex.Message);
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

        _vlcPrepared = true;
        
        prepareCompleted?.Invoke(this);
    }

    private void SwitchToVideoPlayerFallback()
    {
        _usingVLC = false;
        _vlcPrepared = false;
        _unityVideoPlayer.enabled = true;
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _unityVideoPlayer.prepareCompleted += OnUnityVideoPrepared;
        _unityVideoPlayer.seekCompleted += OnUnitySeekCompleted;
    }

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
