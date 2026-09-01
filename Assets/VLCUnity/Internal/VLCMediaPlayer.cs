using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace LibVLCSharp
{
    /// <summary>
    /// This is the core implementation of a media player using VLC for Unity using LibVLCSharp
    /// It exposes basic playback controls, you may wish to add more of these
    /// It outputs audio directly to speakers and video to a RenderTexture
    /// It also handles common problems including vertically flipped videos
    ///
    /// On Android, make sure you require Internet access in your manifest to be able to access internet-hosted videos.
    /// libvlcsharp usage documentation: https://code.videolan.org/videolan/LibVLCSharp/-/blob/master/docs/home.md
    /// LibVLC parameters: https://wiki.videolan.org/VLC_command-line_help/
    /// Report a bug: https://code.videolan.org/videolan/vlc-unity/-/issues
    /// </summary>
    public class VLCMediaPlayer : VLCVideoProviderBase
    {
        private const long MicrosecondsPerMillisecond = 1000;
        private const float BufferingPercentageScale = 100f;

        public static LibVLC LibVLC { get; private set; }
        public MediaPlayer MediaPlayer { get; private set;  }
        public override RenderTexture OutputTexture { get; protected set; }

        [Tooltip("The URL or local file path to the media you want to play.")]
        public string mediaPath = "https://streams.videolan.org/misc/unity-samples/BigBuckBunny.avi";

        [Tooltip("Media-specific LibVLC options (e.g., :start-time=10, :rate=1.5).")]
        public string[] mediaOptions = Array.Empty<string>();

        [Tooltip("Automatically load and play video when the scene starts.")]
        public bool playOnAwake = true;

        [Tooltip("Routes audio through Unity's AudioSource.")]
        public bool useUnityAudio = false;
        public AudioSource targetAudioSource;

        [Tooltip("Flips the output texture horizontally.")]
        public bool flipTextureX = false;
        [Tooltip("Flips the output texture vertically.")]
        public bool flipTextureY = false;

        [Tooltip("Logs high-level activity from this VLCMediaPlayer component. Global sources and outputs are configured separately.")]
        public bool logPlayerActivity = false;

        [Tooltip("Global configuration for LibVLC caching and networking.")]
        public VLCPlayerConfiguration Configuration;

        [Tooltip("Advanced LibVLC command-line options.")]
        public string[] libVLCArguments = Array.Empty<string>();

        public VLCState CurrentState { get; private set; }

        [Tooltip("Invoked whenever the playback state changes.")]
        public VLCPlayerEvent OnPlayerStateChanged = new();

        public event Action<float> OnBuffering;
        public event Action<float> OnPreloadBuffering;

        public override event Action<RenderTexture> OnTextureResized;

        public event Action<string> OnPreloadPrepared;
        public event Action<string> OnPreloadFailed;

        public enum PreloadState { None, Preparing, Prepared }
        public PreloadState CurrentPreloadState { get; private set; }
        public string PreloadedMediaPath { get; private set; }

        private string[] _preloadedOptions = Array.Empty<string>();

        private bool _isBackgroundPlayerReady;
        private bool _isBackgroundBufferFull;

        private MediaPlayer _backgroundNativePlayer;
        private int _cachedVolume = 100;

        private Texture2D _vlcTexture = null;
        private VLCAudioSource _vlcAudioSource;

        private readonly ConcurrentQueue<Action> _mainThreadActions = new();

        #region unity
        private void Awake()
        {
            if (LibVLC == null)
                CreateLibVLC();

            if (useUnityAudio)
            {
                if (targetAudioSource == null)
                {
                    targetAudioSource = gameObject.GetComponent<AudioSource>();
                    if (targetAudioSource == null)
                        targetAudioSource = gameObject.AddComponent<AudioSource>();
                }

                _vlcAudioSource = targetAudioSource.GetComponent<VLCAudioSource>();
                if (_vlcAudioSource == null)
                    _vlcAudioSource = targetAudioSource.gameObject.AddComponent<VLCAudioSource>();
            }

            CreateMediaPlayer();
        }

        private async void Start()
        {
            if (playOnAwake)
                await OpenAsync(mediaPath, mediaOptions);
        }

        private void Update()
        {
            ProcessMainThreadActions();

            if (MediaPlayer == null)
                return;

            uint height = 0;
            uint width = 0;
            MediaPlayer.Size(0, ref width, ref height);

            if (width == 0 || height == 0)
                return;

            if (_vlcTexture == null || _vlcTexture.width != width || _vlcTexture.height != height)
                ResizeOutputTextures(width, height);

            if (_vlcTexture != null)
            {
                if (TextureHelper.UpdateTexture(_vlcTexture, MediaPlayer))
                {
                    var flip = new Vector2(flipTextureX ? -1 : 1, flipTextureY ? -1 : 1);
                    Graphics.Blit(_vlcTexture, OutputTexture, flip, Vector2.zero); // If you wanted to do post processing outside of VLC you could use a shader here.
                }
            }
        }

        private void OnDestroy()
        {
            CancelPreload();

            DestroyMediaPlayer();
            DestroyTextures();
        }
        #endregion

        #region vlc
        public void Open(string path = null, params string[] options)
        {
            PrepareForNewMedia(path);

            var trimmedPath = mediaPath.Trim(new char[] { '"' });
            var finalOptions = options?.Length > 0 ? options : mediaOptions;
            MediaPlayer.Media = new Media(new Uri(trimmedPath), finalOptions);
            Play();
        }

        public async Task OpenAsync(string path = null, params string[] options)
        {
            PrepareForNewMedia(path);

            var finalOptions = options?.Length > 0 ? options : mediaOptions;
            var media = await CreateAndParseMediaAsync(mediaPath, false, finalOptions);

            MediaPlayer.Media = media.SubItems.FirstOrDefault() ?? media;
            Play();
        }

        public async Task PreloadAsync(string path, params string[] options)
        {
            var finalOptions = options?.Length > 0 ? options : mediaOptions;

            if (path == PreloadedMediaPath && finalOptions.SequenceEqual(_preloadedOptions) && CurrentPreloadState != PreloadState.None)
            {
                Log("Ignoring duplicate Preload call.");
                return;
            }

            CancelPreload();

            PreloadedMediaPath = path;
            _preloadedOptions = finalOptions;
            CurrentPreloadState = PreloadState.Preparing;

            _isBackgroundPlayerReady = false;
            _isBackgroundBufferFull = false;

            var player = new MediaPlayer(LibVLC);
            player.Playing += OnBackgroundPlayerReady;
            player.EncounteredError += OnBackgroundPlayerError;
            player.Buffering += OnBackgroundPlayerBuffering;
            _backgroundNativePlayer = player;

            try
            {
                Media media = await CreateAndParseMediaAsync(path, true, finalOptions);

                if (_backgroundNativePlayer != player)
                {
                    media?.Dispose();
                    return;
                }

                player.Media = media.SubItems.FirstOrDefault() ?? media;
                player.Play();
            }
            catch (Exception ex)
            {
                Log($"Failed to preload media. Exception: {ex.Message}");

                var failedPath = PreloadedMediaPath;
                CancelPreload();
                OnPreloadFailed?.Invoke(failedPath);
            }
        }

        public void CancelPreload()
        {
            if (_backgroundNativePlayer == null)
                return;

            _backgroundNativePlayer.Stop();
            _backgroundNativePlayer.Playing -= OnBackgroundPlayerReady;
            _backgroundNativePlayer.EncounteredError -= OnBackgroundPlayerError;
            _backgroundNativePlayer.Buffering -= OnBackgroundPlayerBuffering;
            _backgroundNativePlayer.Media?.Dispose();
            _backgroundNativePlayer.Dispose();
            _backgroundNativePlayer = null;

            PreloadedMediaPath = null;
            _preloadedOptions = Array.Empty<string>();
            CurrentPreloadState = PreloadState.None;
        }

        public void SwapAndPlayNext()
        {
            if (CurrentPreloadState == PreloadState.None || _backgroundNativePlayer == null)
            {
                Log("No preloaded video ready to swap.");
                return;
            }

            if (CurrentPreloadState == PreloadState.Preparing && _backgroundNativePlayer.Media == null)
            {
                Log("Swap requested before parsing finished. Falling back to OpenAsync.");
                var path = PreloadedMediaPath;
                var opts = _preloadedOptions;
                CancelPreload();
                _ = OpenAsync(path, opts);
                return;
            }

            Log("Swapping to preloaded video: " + PreloadedMediaPath);

            DestroyMediaPlayer();

            MediaPlayer = _backgroundNativePlayer;

            if (_vlcAudioSource != null)
                _vlcAudioSource.Attach(MediaPlayer);

            AttachMainPlayerEvents(MediaPlayer);

            _backgroundNativePlayer.Playing -= OnBackgroundPlayerReady;
            _backgroundNativePlayer.EncounteredError -= OnBackgroundPlayerError;
            _backgroundNativePlayer.Buffering -= OnBackgroundPlayerBuffering;
            _backgroundNativePlayer = null;

            mediaPath = PreloadedMediaPath;
            PreloadedMediaPath = null;
            _preloadedOptions = Array.Empty<string>();
            CurrentPreloadState = PreloadState.None;

            DestroyTextures();
            MediaPlayer.SetVolume(_cachedVolume);

            MediaPlayer.Play();
        }

        public void Play()
        {
            Log("VLCMediaPlayer Play");
            MediaPlayer.Play();
        }

        public void Pause()
        {
            Log("VLCMediaPlayer Pause");
            MediaPlayer.Pause();
        }

        public void TogglePlayPause()
        {
            Log("VLCMediaPlayer TogglePlayPause");
            if (IsPlaying)
                Pause();
            else
                Play();
        }

        public void Stop()
        {
            Log("VLCMediaPlayer Stop");
            MediaPlayer?.Stop();
        }

        public void Seek(long timeDelta)
        {
            Log("VLCMediaPlayer Seek " + timeDelta);
            MediaPlayer.SeekTo(TimeSpan.FromMilliseconds(Time + timeDelta));
        }

        public void SetTime(long time)
        {
            Log("VLCMediaPlayer SetTime " + time);
            MediaPlayer.SeekTo(TimeSpan.FromMilliseconds(time));
        }

        public void SetVolume(int volume = 100)
        {
            Log("VLCMediaPlayer SetVolume " + volume);
            _cachedVolume = volume;
            MediaPlayer.SetVolume(volume);
        }

        public int Volume => MediaPlayer != null ? MediaPlayer.Volume : 0;
        public bool IsPlaying => MediaPlayer != null && MediaPlayer.IsPlaying;
        /// <summary>Gets the media duration in milliseconds.</summary>
        public long Duration => (MediaPlayer != null && MediaPlayer.Media != null) ? FromLibVLCTime(MediaPlayer.Media.Duration) : 0;

        /// <summary>Gets the current playback time in milliseconds.</summary>
        public long Time => MediaPlayer != null ? FromLibVLCTime(MediaPlayer.Time) : 0;

        public List<MediaTrack> Tracks(TrackType type)
        {
            return ConvertMediaTrackList(MediaPlayer?.Tracks(type));
        }

        public MediaTrack SelectedTrack(TrackType type)
        {
            return MediaPlayer?.SelectedTrack(type);
        }

        public void Select(MediaTrack track)
        {
            Log("VLCMediaPlayer Select " + track.Name);
            MediaPlayer?.Select(track);
        }

        public void Unselect(TrackType type)
        {
            Log("VLCMediaPlayer Unselect " + type);
            MediaPlayer?.Unselect(type);
        }

        public VideoOrientation? GetVideoOrientation()
        {
            var tracks = MediaPlayer?.Tracks(TrackType.Video);

            if (tracks == null || tracks.Count == 0)
                return null;

            var orientation = tracks[0]?.Data.Video.Orientation; // At the moment we're assuming the track we're playing is the first track

            return orientation;
        }
        #endregion

        #region internal
        //Create a new static LibVLC instance and dispose of the old one. You should only ever have one LibVLC instance.
        void CreateLibVLC()
        {
            if (LibVLC != null)
            {
                VLCUnityLogger.UnhookLibVLC(LibVLC);
                LibVLC.Dispose();
                LibVLC = null;
            }

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_EMBEDDED_LINUX
            Core.Initialize(OnLoad.LibVLCDirectory); // Load bundled Linux libvlc.
#else
            Core.Initialize(Application.dataPath); // Load VLC dlls
#endif

            var args = new List<string>();

            if (Configuration != null)
                args.AddRange(Configuration.GetOptions());

            args.AddRange(libVLCArguments?.Where(arg => !string.IsNullOrWhiteSpace(arg)) ?? Array.Empty<string>());

            LibVLC = new LibVLC(enableDebugLogs: false, args.ToArray()); // You can customize LibVLC with advanced CLI options here https://wiki.videolan.org/VLC_command-line_help/
                                                                        // Setup Error Logging
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            VLCUnityLogger.HookLibVLC(LibVLC);
        }

        private void CreateMediaPlayer()
        {
            if (MediaPlayer != null)
                DestroyMediaPlayer();

            MediaPlayer = new MediaPlayer(LibVLC);

            if (_vlcAudioSource != null)
                _vlcAudioSource.Attach(MediaPlayer);

            AttachMainPlayerEvents(MediaPlayer);
        }

        private void DestroyMediaPlayer()
        {
            MediaPlayer?.Stop();
            MediaPlayer?.Dispose();
            MediaPlayer = null;
        }

        private void DispatchToMainThread(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        private void ProcessMainThreadActions()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                action?.Invoke();
            }
        }

        private void OnStateChange(VLCState newState)
        {
            DispatchToMainThread(() =>
            {
                CurrentState = newState;
                OnPlayerStateChanged?.Invoke(CurrentState);
            });
        }

        private void ResizeOutputTextures(uint px, uint py)
        {
            if (px == 0 || py == 0)
                return;

            DestroyTextures();

            // We have to do this to avoid stretching it.
            if (GetVideoOrientation() == VideoOrientation.BottomRight)
                (py, px) = (px, py);

            _vlcTexture = TextureHelper.CreateNativeTexture(MediaPlayer, linear: true);

            if (_vlcTexture != null)
            {
                OutputTexture = new RenderTexture(_vlcTexture.width, _vlcTexture.height, 0, RenderTextureFormat.ARGB32);
                OutputTexture.Create();

                OnTextureResized?.Invoke(OutputTexture);
            }
        }

        private void DestroyTextures()
        {
            if (OutputTexture != null)
            {
                if (RenderTexture.active == OutputTexture)
                    RenderTexture.active = null;
                OutputTexture.Release();
                DestroyImmediate(OutputTexture);
                OutputTexture = null;
            }

            if (_vlcTexture != null)
            {
                DestroyImmediate(_vlcTexture);
                _vlcTexture = null;
            }
        }

        private void AttachMainPlayerEvents(MediaPlayer player)
        {
            if (player == null)
                return;

            player.Opening += (s, e) => OnStateChange(VLCState.Opening);

            player.Buffering += (s, e) =>
            {
                float cachePercent = ToPercentage(e.Cache);
                DispatchToMainThread(() => OnBuffering?.Invoke(cachePercent));
            };

            player.Playing += (s, e) => OnStateChange(VLCState.Playing);
            player.Paused += (s, e) => OnStateChange(VLCState.Paused);
            player.Stopping += (s, e) => OnStateChange(VLCState.Stopping);
            player.Stopped += (s, e) => OnStateChange(VLCState.Stopped);
            player.EncounteredError += (s, e) => OnStateChange(VLCState.Error);
        }

        private void PrepareForNewMedia(string path)
        {
            if (!string.IsNullOrEmpty(path))
                mediaPath = path;

            Log($"VLCMediaPlayer Opening: {mediaPath}");

            var currentMedia = MediaPlayer.Media;
            currentMedia?.Dispose();
        }

        private async Task<Media> CreateAndParseMediaAsync(string path, bool startPaused, params string[] options)
        {
            options ??= Array.Empty<string>();

            var trimmedPath = path.Trim(new char[] { '"' }); // Windows likes to copy paths with quotes but Uri does not like to open them
            var uri = new Uri(trimmedPath);
            var media = new Media(uri, options);

            if (startPaused)
                media.AddOption(":start-paused");

            var parseOptions = uri.IsFile ? MediaParseOptions.ParseLocal : MediaParseOptions.ParseNetwork;
            await media.ParseAsync(LibVLC, parseOptions);

            return media;
        }

        private void OnBackgroundPlayerReady(object sender, EventArgs e)
        {
            DispatchToMainThread(() =>
            {
                _isBackgroundPlayerReady = true;

                TryFinalizePreload();
            });
        }

        private void TryFinalizePreload()
        {
            if (CurrentPreloadState == PreloadState.Preparing && _isBackgroundPlayerReady && _isBackgroundBufferFull)
            {
                CurrentPreloadState = PreloadState.Prepared;
                OnPreloadPrepared?.Invoke(PreloadedMediaPath);
            }
        }

        private void OnBackgroundPlayerError(object sender, EventArgs e)
        {
            DispatchToMainThread(() =>
            {
                var failedPath = PreloadedMediaPath;
                CancelPreload();
                OnPreloadFailed?.Invoke(failedPath);
            });
        }

        private void OnBackgroundPlayerBuffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            float cachePercent = ToPercentage(e.Cache);

            DispatchToMainThread(() =>
            {
                OnPreloadBuffering?.Invoke(cachePercent);

                if (cachePercent >= 100f && !_isBackgroundBufferFull)
                {
                    _isBackgroundBufferFull = true;
                    TryFinalizePreload();
                }
            });
        }

        private static long FromLibVLCTime(long microseconds) => microseconds / MicrosecondsPerMillisecond;

        private static float ToPercentage(float buffering) => buffering * BufferingPercentageScale;

        //Converts MediaTrackList objects to Unity-friendly generic lists. Might not be worth the trouble.
        List<MediaTrack> ConvertMediaTrackList(MediaTrackList tracklist)
        {
            if (tracklist == null)
                return new List<MediaTrack>();

            var tracks = new List<MediaTrack>((int)tracklist.Count);
            for (uint i = 0; i < tracklist.Count; i++)
            {
                tracks.Add(tracklist[i]);
            }
            return tracks;
        }

        private void Log(object message)
        {
            if (logPlayerActivity)
                VLCUnityLogger.Log($"[VLCMediaPlayer:{name}] {message}");
        }
        #endregion
    }
}
