using System;
using Cysharp.Threading.Tasks;
using LibVLCSharp;
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
    [Header("Fallback")]
    [SerializeField] private VideoPlayer _unityVideoPlayer;

    private VLCMediaPlayer _vlcPlayer;
    private bool _usingVLC = false;
    private bool _vlcPrepared = false;
    private bool _prepareCalled = false;
    private float _prepareStartTime = 0f;
    private const float VLC_PREPARE_TIMEOUT = 5f;

    // Stored values for VLC path (VideoPlayer reads these directly from its component)
    private RenderTexture _targetTexture;
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

    public VideoRenderMode renderMode
    {
        get => _renderMode;
        set
        {
            _renderMode = value;
            if (!_usingVLC && _unityVideoPlayer != null)
                _unityVideoPlayer.renderMode = value;
        }
    }

    public RenderTexture targetTexture
    {
        get => _targetTexture;
        set
        {
            _targetTexture = value;
            Debug.Log($"[YargVideoPlayer] targetTexture set to {value}");
            if (_usingVLC && _vlcPlayer != null && value != null)
            {
                _vlcPlayer.SetExternalOutputTexture(value);
            }
            else if (!_usingVLC && _unityVideoPlayer != null)
            {
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
            if (_usingVLC && _vlcPlayer != null)
                _vlcPlayer.SetTime((long)(value * 1000));
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

    private void Awake()
    {
        if (_unityVideoPlayer == null)
            _unityVideoPlayer = GetComponent<VideoPlayer>();
    }

    private void Start()
    {
        TryInitializeVLC();
    }

    private void Update()
    {
        if (_usingVLC && _vlcPlayer != null)
        {
            if (!_vlcPrepared && _prepareCalled && Time.time - _prepareStartTime > VLC_PREPARE_TIMEOUT)
            {
                Debug.LogWarning($"[YargVideoPlayer] VLC texture not available after timeout (OutputTexture={_vlcPlayer.OutputTexture}, targetTexture={_targetTexture}), falling back to VideoPlayer");
                SwitchToVideoPlayerFallback();
            }
        }
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
        if (!VLCMediaPlayer.IsVLCAvailable)
        {
            _usingVLC = false;
            Debug.Log("[YargVideoPlayer] VLC not available, using Unity VideoPlayer");
            return;
        }

        try
        {
            var go = new GameObject("VLCPlayer");
            go.transform.SetParent(transform, false);
            _vlcPlayer = go.AddComponent<VLCMediaPlayer>();
            _vlcPlayer.playOnAwake = false;
            _vlcPlayer.useUnityAudio = false;
            _vlcPlayer.flipTextureX = true;
            _vlcPlayer.flipTextureY = true;
            _vlcPlayer.OnTextureResized += OnVLCTextureResized;

            if (!_vlcPlayer.enabled)
            {
                // Awake disabled it — VLC init failed
                throw new InvalidOperationException("VLC player was disabled during initialization");
            }

            _usingVLC = true;
            Debug.Log("[YargVideoPlayer] VLC initialized successfully");
            
            // If targetTexture was set before VLC was available, pass it now
            if (_targetTexture != null)
            {
                _vlcPlayer.SetExternalOutputTexture(_targetTexture);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[YargVideoPlayer] VLC initialization failed, falling back to Unity VideoPlayer: " + ex.Message);
            _usingVLC = false;
            if (_vlcPlayer != null)
            {
                Destroy(_vlcPlayer.gameObject);
                _vlcPlayer = null;
            }
        }
    }

    private void OnVLCTextureResized(RenderTexture texture)
    {
        _vlcPrepared = true;
        prepareCompleted?.Invoke(this);
    }

    private void SwitchToVideoPlayerFallback()
    {
        _usingVLC = false;
        _vlcPrepared = false;
        if (_vlcPlayer != null)
        {
            _vlcPlayer.SetExternalOutputTexture(null);
        }
        _unityVideoPlayer.enabled = true;
        _unityVideoPlayer.url = _url;
        _unityVideoPlayer.renderMode = _renderMode;
        _unityVideoPlayer.targetTexture = _targetTexture;
        _unityVideoPlayer.prepareCompleted += OnUnityVideoPrepared;
        _unityVideoPlayer.Prepare();
    }

    private void OnUnityVideoPrepared(VideoPlayer vp)
    {
        _unityVideoPlayer.prepareCompleted -= OnUnityVideoPrepared;
        prepareCompleted?.Invoke(this);
    }
}
