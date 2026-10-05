using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Cinemachine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniHumanoid;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UI;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Venue;
using YARG.Helpers.Extensions;
using YARG.Settings;
using YARG.Venue;
using YARG.Venue.Characters;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Song;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace YARG.Gameplay
{
    public class BackgroundManager : GameplayBehaviour, IDisposable
    {
        // e.g. DefaultController.Vocals.Rock.controller
        private const string DEFAULT_ANIMATION_CONTROLLER_PATH     = "Animations/{0}/{1}/";
        private const string DEFAULT_ANIMATION_CONTROLLER_FILENAME = "DefaultController";
        private const string DEFAULT_ANIMATION_PARAMETERS_FILENAME = "AnimatorParameters";

        private string VIDEO_PATH;

        [SerializeField]
        private YargVideoPlayer _videoPlayer;

        [SerializeField]
        private RawImage _backgroundImage;

        [SerializeField]
        private Image _backgroundDimmer;

        [SerializeField]
        private RawImage _venueOutput;

        [SerializeField]
        private Image _venueFadeOverlay;

        private BackgroundType _type;
        private VenueSource _source;

        private bool _videoStarted = false;
        private bool _videoSeeking = false;
        private bool _videoSeekWaitForPause = false;

        private const float FADE_DURATION = 0.5f;

        // Load-bearing for video sync, not a tuning knob. libavcodec's frame threading delays
        // decoder output by thread_count - 1 frames by design and hardware decoders buffer more on
        // top; at 24-30fps that delay is the whole of the offset the video would otherwise sit at,
        // and no seek or resume timing shifts it. Per-media because LibVLC is process-wide and
        // built once; serialized so single-threaded decode can be turned off on a machine it
        // starves.
        [Header("Video decoder latency")]
        [SerializeField]
        [Tooltip("Single-threaded decode. Removes libavcodec's frame-threading output delay of " +
                 "thread_count - 1 frames, at the cost of decode throughput.")]
        private bool _decoderSingleThreaded = true;

        [SerializeField]
        [Tooltip("Minimise delay through demux, packetisation and decode.")]
        private bool _decoderLowDelay = true;

        // libVLC maps its first picture after a seek to "now + input caching" in wall time while
        // the stream advances at the song's rate, so at rate r the video settles about
        // caching x (r - 1) off: -0.21s at 120% with the default 1000ms. Local files need little.
        [SerializeField]
        [Tooltip("libVLC input caching for the video file, in ms (:file-caching). The video " +
                 "settles about this x (speed - 1) off at non-100% speeds; 0 leaves libVLC's " +
                 "default (1000ms).")]
        private int _fileCachingMs = 100;

        // Pictures the decoder must deliver after the song-start seek before the curtain lifts.
        private const long REVEAL_MIN_FRAMES = 3;
        private bool _videoRevealed;
        private bool _videoEnding;
        private long _revealFrameBaseline;

        private float YARGROUND_OFFSET = 50f;

        private readonly List<AsyncOperationHandle<GameObject>> _handles = new();
        private          bool                                   loadedAddressable;

        // These values are relative to the video, not to song time!
        // A negative start time will delay when the video starts, a positive one will set the video position
        // to that value when starting playback at the start of a song.
        private double _videoStartTime;

        // Wall-clock seconds the video is aimed ahead of the song; see VideoLeadFor.
        private double _videoLeadSeconds;

        // Pausing parks the video on the frame the resume will rewind to, so resuming is a Play
        // rather than a seek: a seek from a running song lands late by however long the decoder
        // takes to reach the target from its keyframe. See TryParkForResume.
        //
        // Seeking: the park seek is in flight, video still playing. Parked: landed and paused.
        // Releasing: the song has resumed; Play once it reaches the parked frame.
        private enum ParkState { None, Seeking, Parked, Releasing }
        private ParkState _parkState;
        private double _parkedAt;

        // Seeks are issued at rate 1 (see SeekVideo); a seek on a running video takes the song's
        // rate back once it lands.
        private bool _applyRateOnSeekLanded;

        // A speed step on a playing video re-parks it once the steps settle (see SetSpeed).
        private float _reparkAtUnscaledTime = float.NaN;
        private bool _releaseParkOnLanding;
        private const float SPEED_STEP_SETTLE_SECONDS = 0.3f;
        private const double REPARK_MARGIN_SECONDS = 0.4;

        // End time cannot be negative; a negative value means it is not set.
        private double _videoEndTime;

        private AssetBundle _characterBundle;

        private BundleBackgroundManager _bundleBackgroundManager;

#if UNITY_EDITOR
        private          bool             _usingEditorVenue;
        private          string           _editorVenuePath;
        private          Scene            _editorVenueScene;
#endif
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            // We don't need to update unless we're using a video
            enabled = false;

#if UNITY_EDITOR
            if (VenueEditorHelper.IsSceneEnabled())
            {
                if (VenueEditorHelper.TryGetScenePath(out _editorVenuePath))
                {
                    var loadedScene = SceneManager.GetSceneByName(_editorVenuePath);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                    {
                        _editorVenueScene = loadedScene;
                    }
                    else
                    {
                        var op = EditorSceneManager.LoadSceneAsyncInPlayMode(
                            _editorVenuePath, new LoadSceneParameters(LoadSceneMode.Additive));

                        await op;
                        _editorVenueScene = SceneManager.GetSceneByPath(_editorVenuePath);
                    }
                }

                if (!_editorVenueScene.IsValid() || !_editorVenueScene.isLoaded)
                {
                    YargLogger.LogFormatError("Failed to load editor venue scene {0}", _editorVenuePath);
                    return;
                }

                BundleBackgroundManager editorBg = null;
                foreach (var go in _editorVenueScene.GetRootGameObjects())
                {
                    editorBg = go.GetComponent<BundleBackgroundManager>();

                    if (editorBg != null)
                    {
                        break;
                    }
                }

                if (editorBg == null)
                {
                    YargLogger.LogFormatError("Scene {0} missing BundleBackgroundManager", _editorVenuePath);
                    return;
                }

                _usingEditorVenue = true;

                ShowVenue();

                var editorRenderers = editorBg.GetComponentsInChildren<Renderer>(true);

                // Song specific textures
                var tm = GetComponent<TextureManager>();
                var songBg = GameManager.Song.LoadBackground(true);

                foreach (var renderer in editorRenderers)
                {
                    var materials = renderer.materials;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        tm.ProcessMaterial(materials[i], songBg?.Type);
                    }

                    renderer.materials = materials;
                }

                editorBg.SetupVenueCamera(editorBg.gameObject);
                editorBg.LimitVenueLights(editorBg.gameObject);

                if (_videoPlayer != null && _videoPlayer.targetCamera != null)
                {
                    Destroy(_videoPlayer.targetCamera.gameObject);
                }

                _type = BackgroundType.Yarground;

                // Initialize CharacterManager, if it exists
                var characterManager = editorBg.GetComponentInChildren<CharacterManager>();
                if (characterManager != null)
                {
                    characterManager.Initialize();
                }

                return;
            }
#endif

            using var result = VenueLoader.GetVenue(GameManager.Song, out _source);

            if (result == null)
            {
                return;
            }

            var vocalGender = GameManager.Song.VocalGender;

            var colorDim = _backgroundDimmer.color.WithAlpha(1 - SettingsManager.Settings.SongBackgroundOpacity.Value);

            _backgroundDimmer.color = colorDim;

            // If we have a venue hint for the song and we can load the hinted yarground, prefer that
            var hint = GameManager.Song.VenueHint;
            if (!string.IsNullOrWhiteSpace(hint))
            {
                if (await AddressableVenueExists(hint))
                {
                    var loaded = await LoadAddressableYarground(hint, vocalGender);
                    if (loaded)
                    {
                        GameManager.CrowdEventHandler.Start();
                        return;
                    }
                }
            }

            // Hint didn't resolve or failed to load, so pretend it didn't exist

            _type = result.Type;

            // Start crowd event handler now if we aren't waiting on a yarground
            // TODO: Figure out how to decouple this
            if (_type != BackgroundType.Yarground)
            {
                GameManager.CrowdEventHandler.Start();
            }

            switch (_type)
            {
                case BackgroundType.Yarground:
                    await LoadYarground(result);
                    GameManager.CrowdEventHandler.Start();
                    break;
                case BackgroundType.Video:
                    LoadVideoBackground(result);
                    break;
                case BackgroundType.Image:
                    _backgroundImage.texture = result.Image.LoadTexture(false);
                    _backgroundImage.uvRect = new Rect(0f, 0f, 1f, -1f);
                    _backgroundImage.gameObject.SetActive(true);
                    break;
            }
        }

        private async UniTask<bool> AddressableVenueExists(string hint)
        {
            const string venueLabel = "venue";

            var venueKeys = await Addressables.LoadResourceLocationsAsync(venueLabel);
            foreach (var location in venueKeys)
            {
                if (location.PrimaryKey == hint)
                {
                    return true;
                }
            }

            return false;
        }

        private async UniTask LoadAddressableYarground(VocalGender gender = VocalGender.Unspecified)
        {
            const string venueLabel = "venue";
            var venueKeys = await Addressables.LoadResourceLocationsAsync(venueLabel);
            if (venueKeys.Count > 0)
            {
                var location = venueKeys[Random.Range(0, venueKeys.Count)];
                var key = location.PrimaryKey;
                await LoadAddressableYarground(key, gender);
            }
            else
            {
                YargLogger.LogWarning("No addressable venues exist!");
            }

            Addressables.Release(venueKeys);
        }

        private async UniTask<bool> LoadAddressableYarground(string key, VocalGender gender)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            await handle;
            if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
            {
                _handles.Add(handle);
                loadedAddressable = true;
            }
            else
            {
                // We failed, so don't do anything except log a warning
                Debug.LogWarning("Failed to load addressable background");
                return false;
            }

            var bg = handle.Result;

            await LoadCustomAudioAssetsAddressable(key);
            await LoadYargroundPrefab(bg, gender);

            return true;
        }

        private async UniTask LoadYarground(BackgroundResult result)
        {
            var bundle = AssetBundle.LoadFromStream(result.Stream);
            AssetBundle shaderBundle = null;

            // KEEP THIS PATH LOWERCASE
            // Breaks things for other platforms, because Unity
            var bg = (GameObject) await bundle.LoadAssetAsync<GameObject>(
                BackgroundHelper.BACKGROUND_PREFAB_PATH.ToLowerInvariant());

            // Load Metal shaders, if necessary
            shaderBundle = BackgroundHelper.LoadMetalShaders(bundle, bg, BackgroundHelper.ExportType.Background);

            // Load custom audio
            await LoadCustomAudioAssets(bg, bundle);

            var gender = GameManager.Song.VocalGender;
            await LoadYargroundPrefab(bg, gender, manager =>
            {
                manager.Bundle = bundle;
                manager.ShaderBundles.Add(shaderBundle);
            });
        }

        private async UniTask LoadYargroundPrefab(GameObject bg, VocalGender gender,
            Action<BundleBackgroundManager> callback = null)
        {
            ShowVenue();

            var renderers = bg.GetComponentsInChildren<Renderer>(true);

            var textureManager = GetComponent<TextureManager>();
            var songBackground = GameManager.Song.LoadBackground(SettingsManager.Settings.CensorMatureContent.Value);

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    textureManager.ProcessMaterial(material, songBackground?.Type);
                }
            }

            var bgInstance = Instantiate(bg);
            var bundleBackgroundManager = bgInstance.GetComponent<BundleBackgroundManager>();

            callback?.Invoke(bundleBackgroundManager);

            bundleBackgroundManager.SetupVenueCamera(bgInstance);
            bundleBackgroundManager.LimitVenueLights(bgInstance);

            _bundleBackgroundManager = bundleBackgroundManager;

            // Position venue as close to origin as conveniently possible
            SetYargroundOrigin(bgInstance);

            // Destroy default camera (venue has its own)
            Destroy(_videoPlayer.targetCamera.gameObject);

            if (textureManager.VideoTexFound())
            {
                SetUpVideoTexture(songBackground);
            }

            var hint = GameManager.Song.VocalCharacterHint;
            var usingCustomChar = await LoadCharacter(bgInstance, hint, gender);


            // Initialize CharacterManager, if it exists
            var characterManager = bgInstance.GetComponentInChildren<CharacterManager>();
            if (characterManager != null)
            {
                characterManager.Initialize(usingCustomChar);
            }
        }

        // Loads all audio assets from the given locations
        private static async UniTask LoadCustomAudioAssetsAddressable(string baseKey)
        {
            if (!SettingsManager.Settings.UseVenueSfx.Value)
            {
                return;
            }

            var locations = Addressables.ResourceLocators;

            var sfxAssets = new Dictionary<string, byte[]>();
            foreach (var location in locations)
            {
                foreach (var key in location.Keys)
                {
                    if (key is not string k)
                    {
                        continue;
                    }

                    // Check if location.PrimaryKey ends with anything in BackgroundHelper.AUDIO_FILE_EXTENSIONS
                    if (k.StartsWith(baseKey)
                        && BackgroundHelper.AUDIO_FILE_EXTENSIONS
                            .Any(s => k.EndsWith(s + ".bytes", StringComparison.OrdinalIgnoreCase)))
                    {
                        var asset = await Addressables.LoadAssetAsync<TextAsset>(k);
                        var sampleName = Path.GetFileNameWithoutExtension(k);
                        sfxAssets.Add(sampleName, asset.bytes);
                        // This should be fine since we are holding on to the venue asset itself so we shouldn't
                        // be unloading and reloading the underlying AssetBundle repeatedly
                        Addressables.Release(asset);
                    }
                }
            }

            if (sfxAssets.Count > 0)
            {
                CustomSFX.AddClips(sfxAssets);
            }
        }

        private static async UniTask LoadCustomAudioAssets(GameObject bg, AssetBundle bundle)
        {
            if (!SettingsManager.Settings.UseVenueSfx.Value)
            {
                return;
            }

            var customSfx = bg.GetComponentInChildren<CustomSFX>();
            if (customSfx != null)
            {
                var assetPaths = bundle.GetAllAssetNames();
                var sfxAssets = new Dictionary<string, byte[]>();
                foreach (var assetPath in assetPaths)
                {
                    if (!assetPath.Contains(BackgroundHelper.AUDIO_PATH.ToLowerInvariant()))
                    {
                        continue;
                    }

                    if (BackgroundHelper.AUDIO_FILE_EXTENSIONS.Any(s => assetPath.EndsWith(s + ".bytes", StringComparison.OrdinalIgnoreCase)))
                    {
                        var sampleName = Path.GetFileNameWithoutExtension(assetPath);
                        if (!sfxAssets.ContainsKey(assetPath))
                        {
                            var audioAsset = (TextAsset) await bundle.LoadAssetAsync<TextAsset>(assetPath);
                            sfxAssets.Add(sampleName, audioAsset.bytes);
                        }
                    }
                }

                if (sfxAssets.Count > 0)
                {
                    CustomSFX.AddClips(sfxAssets);
                }
            }
        }

        private void SetUpVideoTexture(BackgroundResult songBackGround)
        {
            var textureManager = GetComponent<TextureManager>();
            textureManager.CreateVideoTexture();
            if (songBackGround == null || songBackGround.Type == BackgroundType.Yarground)
            {
                return;
            }
            switch (songBackGround.Type)
            {
                case BackgroundType.Video:
                    //set venue source to song to enable video seeking/pausing features
                    _source = VenueSource.Song;
                    //set up videoPlayer to render to venue texture
                    _videoPlayer.targetTexture = textureManager.GetVideoTexture(0, 0);

                    LoadVideoBackground(songBackGround);
                    break;
                case BackgroundType.Image:
                    var songTex = songBackGround.Image.LoadTexture(false);
                    //render image background flipped to match video
                    Graphics.Blit(songTex, textureManager.GetVideoTexture(0, 0), new Vector2(1, -1), new Vector2(0, 1));
                    //clean up unused texture
                    Destroy(songTex);
                    return;
            }
        }

        private void ShowVenue()
        {
            _venueOutput.gameObject.SetActive(true);
            FadeInVenue().Forget();
        }

        private async UniTaskVoid FadeInVenue()
        {
            // Wait for the venue to be rendered, otherwise we will see a gray screen
            var token = this.GetCancellationTokenOnDestroy();
            await UniTask
                .WaitUntil(
                    () => VenueCameraRenderer.IsRendered,
                    cancellationToken: token
                )
                .SuppressCancellationThrow();
            _venueFadeOverlay.CrossFadeAlpha(0f, FADE_DURATION, true);
        }

        private void LoadVideoBackground(BackgroundResult bg)
        {
            var textureManager = GetComponent<TextureManager>();

            var videoTexture = textureManager.GetVideoTexture(Screen.width, Screen.height);
            textureManager.CreateVideoTexture();
            _videoPlayer.targetTexture = videoTexture;

            switch (bg.Stream)
            {
                case FileStream fs:
                {
                    _videoPlayer.url = fs.Name;
                    break;
                }
                case SngFileStream sngStream:
                {
                    // UNFORTUNATELY, Videoplayer can't use streams, so video files
                    // MUST BE FULLY DECRYPTED

                    VIDEO_PATH = Path.Combine(Application.persistentDataPath, sngStream.Name);
                    using var tmp = File.OpenWrite(VIDEO_PATH);
                    File.SetAttributes(VIDEO_PATH, File.GetAttributes(VIDEO_PATH) | FileAttributes.Temporary | FileAttributes.Hidden);
                    bg.Stream.CopyTo(tmp);
                    _videoPlayer.url = VIDEO_PATH;
                    break;
                }
            }

            _videoPlayer.playerEnabled = true;
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.seekCompleted += OnVideoSeeked;
            _videoPlayer.Prepare(BuildMediaOptions());
            enabled = true;
        }

        private void Update()
        {
            if (_videoSeeking)
                return;

            double time = GameManager.GetVideoPlaybackTime();

            if (_parkState == ParkState.Releasing)
                ReleaseParkedVideo(time);

            if (!float.IsNaN(_reparkAtUnscaledTime) && Time.unscaledTime >= _reparkAtUnscaledTime)
            {
                _reparkAtUnscaledTime = float.NaN;
                ReparkForSpeed(time);
            }

            // Start video
            if (!_videoStarted)
            {
                // Don't start playing the video until the start of the song
                if (time < 0.0)
                    return;

                // Delay until the start time is reached
                if (_source == VenueSource.Song && VideoTargetFor(time) < 0.0)
                    return;

                if (_videoEndTime == 0)
                    return;

                _videoStarted = true;

                // At a non-100% speed a held load-time seek (practice entry) has parked the video
                // on this frame; releasing it takes the rate while paused. A running re-seek would
                // have to change the rate on a playing video instead.
                bool releaseLoadPark = _source == VenueSource.Song && _videoPlayer.usingVlc &&
                    !Mathf.Approximately(GameManager.SongSpeed, 1f) &&
                    _parkState is ParkState.Parked or ParkState.Releasing;

                if (releaseLoadPark)
                {
                    if (_parkState == ParkState.Parked)
                        ArmParkRelease();
                }
                else
                {
                    _parkState = ParkState.None;
                    SetVideoPlaying(true);
                }

                if (_source == VenueSource.Song && !releaseLoadPark)
                {
                    // Re-seek even when the player already sits on the target: at 100% a parked
                    // start (played rather than re-seeked) settles 20-40ms later on videos that
                    // start on a keyframe. Must follow the Play above, because a seek issued while
                    // paused is dropped.
                    double startTarget = VideoTargetFor(time);
                    SeekVideo(startTarget);
                    _applyRateOnSeekLanded = true;
                }

                _revealFrameBaseline = _videoPlayer.framesDelivered;

                // Disable after starting the video if it's not from the song folder
                // or if video end time is not specified
                if (_source != VenueSource.Song || double.IsNaN(_videoEndTime))
                {
                    // Update stops here, so there is no later chance to reveal.
                    RevealVideoWhenPlaying(force: true);

                    enabled = false;
                    return;
                }
            }

            RevealVideoWhenPlaying(force: false);

            // End video when reaching the specified end time, behind the curtain: what a stopped
            // player leaves on the texture is undefined.
            double videoPosition = time + _videoStartTime;
            if (!_videoEnding && videoPosition >= _videoEndTime - FADE_DURATION * GameManager.SongSpeed)
                BeginVideoFadeOut();

            if (videoPosition >= _videoEndTime)
            {
                _videoPlayer.Stop();
                _videoPlayer.playerEnabled = false;
                enabled = false;
            }
        }

        private void BeginVideoFadeOut()
        {
            _videoEnding = true;

            // Plain-video backgrounds only, as for the reveal.
            if (_type == BackgroundType.Video)
                _venueFadeOverlay.CrossFadeAlpha(1f, FADE_DURATION, true);
        }

        // Some video player properties don't work correctly until
        // it's finished preparing, such as the length
        private void OnVideoPrepared(YargVideoPlayer player)
        {
            // Start time is considered set if it is greater than 25 ms in either direction
            // End time is only set if it is greater than 0
            // Video will only loop if its length is less than 85% of the song's length
            const double startTimeThreshold = 0.025;
            const double endTimeThreshold = 0;
            const double dontLoopThreshold = 0.85;

            if (_source == VenueSource.Song && !GameManager.Song.VideoLoop)
            {
                _videoStartTime = GameManager.Song.VideoStartTimeSeconds;
                _videoEndTime = GameManager.Song.VideoEndTimeSeconds;
                _videoLeadSeconds = VideoLeadFor(player);

                // Clamped: a negative start time delays when the video starts; it does not name a
                // position before the file begins.
                SeekVideo(Math.Max(VideoTargetFor(0.0), 0.0));
                if (!player.usingVlc)
                    player.playbackSpeed = GameManager.SongSpeed;

                // Only loop the video if it's not around the same length as the song
                if (Math.Abs(_videoStartTime) < startTimeThreshold &&
                    _videoEndTime <= endTimeThreshold &&
                    player.length < GameManager.SongLength * dontLoopThreshold)
                {
                    player.isLooping = true;
                    _videoEndTime = double.NaN;
                }
                else
                {
                    player.isLooping = false;
                    if (_videoEndTime <= 0)
                    {
                        // Unspecified: the video's own end, or the song's if the video is longer,
                        // which it otherwise plays on past.
                        _videoEndTime = Math.Min(player.length, GameManager.SongLength + _videoStartTime);
                    }
                }
            }
            else
            {
                _videoStartTime = 0;
                _videoEndTime = double.NaN;
                player.isLooping = true;
            }

            // The player arrives here still playing, and the start-position seek above only lands
            // because of that -- a seek issued while paused is dropped. Pause now, after the seek,
            // or the video runs ahead of its mark until Update()'s start block.
            SetVideoPlaying(false);

            GetComponent<TextureManager>().SetVideoTexture(_videoPlayer.targetTexture);
            if (_type == BackgroundType.Video)
            {
                // Bound and live, but behind the curtain until RevealVideoWhenPlaying.
                _venueOutput.texture = _videoPlayer.targetTexture;
                _venueOutput.gameObject.SetActive(true);
            }
        }

        public void SetTime(double songTime, bool waitForSeek = true)
        {
            switch (_type)
            {
                case BackgroundType.Video:
                    // Don't seek videos that aren't from the song
                    if (_source != VenueSource.Song)
                        return;

                    _parkState = ParkState.None;
                    _reparkAtUnscaledTime = float.NaN;
                    _releaseParkOnLanding = false;

                    // A seek back into a video that has faded out (practice) reveals it again.
                    if (_videoEnding)
                    {
                        _videoEnding = false;
                        _videoRevealed = false;
                        _revealFrameBaseline = _videoPlayer.framesDelivered;
                    }

                    double videoTime = VideoTargetFor(songTime);
                    if (videoTime < 0f) // Seeking before video start
                    {
                        enabled = true;
                        _videoPlayer.playerEnabled = true;
                        _videoStarted = false;
                        _videoPlayer.Stop();
                    }
                    else if (videoTime >= _videoPlayer.length) // Seeking after video end
                    {
                        enabled = false;
                        _videoPlayer.playerEnabled = false;
                        _videoPlayer.Stop();
                    }
                    else
                    {
                        enabled = false; // Temp disable
                        _videoPlayer.playerEnabled = true;

                        // Hack to ensure the video stays synced to the audio
                        _videoSeeking = true; // Signaling flag; must come first
                        _videoSeekWaitForPause = waitForSeek;
                        bool videoWasPaused = _videoPlayer.isPaused;

                        if (waitForSeek && SettingsManager.Settings.WaitForSongVideo.Value)
                            GameManager.OverridePause();

                        // A seek issued while paused is dropped, so play across it; OnVideoSeeked
                        // settles the video back into the song's state once it lands.
                        if (videoWasPaused)
                            SetVideoPlaying(true);

                        SeekVideo(videoTime);
                        _applyRateOnSeekLanded = true;
                    }
                    break;
            }
        }

        private void OnVideoSeeked(YargVideoPlayer player)
        {
            if (_parkState == ParkState.Seeking)
            {
                _parkState = ParkState.Parked;
                SetVideoPlaying(false);

                // A speed re-park releases on its own; a pause's park waits for the resume.
                if (_releaseParkOnLanding)
                {
                    _releaseParkOnLanding = false;
                    ArmParkRelease();
                }

                return;
            }

            // A held seek (SetTime) at a non-100% speed parks instead of taking the rate while
            // playing: changing the rate of a playing video jumps it by (time since its new clock
            // context began) x delta-rate, which measured 50-100ms even right after landing.
            bool parkHeldSeek = _videoSeeking && _source == VenueSource.Song && _videoPlayer.usingVlc &&
                !Mathf.Approximately(GameManager.SongSpeed, 1f);

            // Otherwise its first picture has only just started libVLC's new clock context, so the
            // rate change costs little.
            if (_applyRateOnSeekLanded)
            {
                _applyRateOnSeekLanded = false;
                if (!parkHeldSeek)
                    ApplyVideoRate();
            }

            if (!_videoSeeking)
                return;

            if (_videoSeekWaitForPause && SettingsManager.Settings.WaitForSongVideo.Value)
                GameManager.OverrideResume();

            if (parkHeldSeek)
            {
                // Released by ReleaseParkedVideo, which applies the rate while paused, once the song
                // reaches the picture; if the song is still paused, the resume arms it instead.
                SetVideoPlaying(false);
                _parkState = ParkState.Parked;
                if (!GameManager.Paused)
                    ArmParkRelease();
            }
            else
            {
                // Follow the song, not the video's state before the seek: a resume issued while the
                // seek was in flight skipped the video (SetPaused ignores it mid-seek), and a pause
                // could have landed meanwhile too.
                SetVideoPlaying(!GameManager.Paused);
            }

            enabled = !double.IsNaN(_videoEndTime);
            _videoSeeking = false;
            _videoSeekWaitForPause = false;
        }

        private string[] BuildMediaOptions()
        {
            var options = new List<string>();

            if (_decoderSingleThreaded)
                options.Add(":avcodec-threads=1");

            if (_decoderLowDelay)
                options.Add(":low-delay");

            if (_fileCachingMs > 0)
                options.Add($":file-caching={_fileCachingMs}");

            // Insurance, not a tuning knob: background videos are silent, and a badly muxed one
            // must not reach the speakers. It is not a sync lever -- VLC only makes the audio
            // output its master clock when a track exists, and these have none.
            options.Add(":no-audio");

            // Never ":start-time=". It decouples the player's reported time from the picture it
            // is showing, so the video runs seconds out while every sync metric here -- all of
            // which derive from MediaPlayer.Time -- still reads correct.

            return options.ToArray();
        }

        // VLC's pictures reach the screen a fixed interval after libVLC presents them, so a video
        // aimed exactly at the song shows late; Unity's player has no such lag. About two 24fps
        // frames on macOS and Linux. Not derived from the video's frame rate: libVLC reports 0 fps
        // for fragmented MP4s (which is fairly common), whose headers carry no sample count.
        private static double VideoLeadFor(YargVideoPlayer player)
        {
            return player.usingVlc ? 0.08 : 0.0;
        }

        // The video position to aim at for a song time. The lead is wall-clock, so it scales with
        // song speed when expressed in video time.
        private double VideoTargetFor(double songTime)
        {
            return songTime + _videoStartTime + _videoLeadSeconds * GameManager.SongSpeed;
        }

        // Lifts the curtain once the decoder has delivered pictures, so a slow spin-up is hidden
        // rather than shown. Latched.
        private void RevealVideoWhenPlaying(bool force)
        {
            // Not while ending: the reveal would cancel the fade-out it runs every frame against.
            if (_videoRevealed || _videoEnding)
                return;

            if (!force && _videoPlayer.framesDelivered - _revealFrameBaseline < REVEAL_MIN_FRAMES)
                return;

            _videoRevealed = true;

            // Plain-video backgrounds only: a yarground's embedded screen shares this manager but
            // gets its reveal from ShowVenue().
            if (_type == BackgroundType.Video)
                _venueFadeOverlay.CrossFadeAlpha(0f, FADE_DURATION, true);
        }

        // Every play/pause goes through here. Several independent paths drive the player, and
        // Play/Pause are applied asynchronously in issue order, so the last one issued wins rather
        // than the last intended.
        private void SetVideoPlaying(bool playing)
        {
            if (playing)
            {
                _videoPlayer.Play();
            }
            else
            {
                _videoPlayer.Pause();
            }
        }

        // Called as the song resumes from a pause's rewind. A parked video plays once the song
        // reaches its frame; otherwise this falls back to the song-start block's Play-then-seek,
        // which lands late by the decode time from the target's keyframe.
        public void ResyncVideoToSong()
        {
            if (_type != BackgroundType.Video || _source != VenueSource.Song ||
                !_videoStarted || _videoSeeking || !_videoPlayer.playerEnabled)
            {
                return;
            }

            double time = GameManager.GetVideoPlaybackTime();
            double target = VideoTargetFor(time);

            // Outside the video's own span, the start block and end check already own it.
            if (target < 0 || target >= _videoPlayer.length)
                return;

            // A landed park already holds (about) this frame: play it when the song gets there.
            // Measured from the picture on screen, which is what playback resumes from; the paused
            // player's reported time runs ahead of it.
            double parkedAt = _videoPlayer.presentedPictureTime;
            if (double.IsNaN(parkedAt))
                parkedAt = _videoPlayer.time;

            if (_parkState == ParkState.Parked && Math.Abs(parkedAt - target) < PARK_MAX_MISMATCH_SECONDS)
            {
                _parkedAt = parkedAt;
                _parkState = ParkState.Releasing;
                return;
            }

            _parkState = ParkState.None;

            SetVideoPlaying(true);
            SeekVideo(target);
            _applyRateOnSeekLanded = true;
        }

        // A park more than this far from the resume target is from a different rewind.
        private const double PARK_MAX_MISMATCH_SECONDS = 0.25;

        // Seeks while still playing -- a seek issued while paused is dropped -- and pauses once it
        // lands, in OnVideoSeeked. Where it stops needn't be exact: the release reads back the
        // picture actually parked on and waits for the song to reach it.
        private bool TryParkForResume(double playbackTime)
        {
            if (_type != BackgroundType.Video || _source != VenueSource.Song ||
                !_videoStarted || _videoSeeking || !_videoPlayer.playerEnabled || !_videoPlayer.usingVlc)
            {
                return false;
            }

            double target = VideoTargetFor(playbackTime);
            if (target < 0 || target >= _videoPlayer.length)
                return false;

            _parkState = ParkState.Seeking;
            SetVideoPlaying(true);
            SeekVideo(target);
            return true;
        }

        private void ArmParkRelease()
        {
            _parkedAt = _videoPlayer.presentedPictureTime;
            _parkState = ParkState.Releasing;
        }

        private void ReleaseParkedVideo(double time)
        {
            // The picture the paused video will resume from; it can still advance a frame or two
            // after the pause is issued.
            double picture = _videoPlayer.presentedPictureTime;
            if (!double.IsNaN(picture))
                _parkedAt = picture;

            if (GameManager.Paused || VideoTargetFor(time) < _parkedAt)
                return;

            _parkState = ParkState.None;

            // Paused, with the next picture starting a new clock context: the one moment a rate
            // change costs nothing.
            ApplyVideoRate();
            SetVideoPlaying(true);
        }

        // A seek lands on target only when issued at rate 1; at rate r it lands off by about
        // target x (r - 1). So every VLC seek drops to 1, and ApplyVideoRate restores the song's
        // rate once the seek has landed or the parked video is released.
        private void SeekVideo(double target)
        {
            if (_videoPlayer.usingVlc)
                _videoPlayer.playbackSpeed = 1f;

            _videoPlayer.time = target;
        }

        private void ApplyVideoRate()
        {
            float speed = GameManager.SongSpeed;
            if (Mathf.Approximately(_videoPlayer.playbackSpeed, speed))
                return;

            _videoPlayer.playbackSpeed = speed;
        }

        // Seeks a little ahead at rate 1, parks there, and releases at the new rate when the song
        // arrives. The video holds that frame for about the margin.
        private void ReparkForSpeed(double time)
        {
            if (GameManager.Paused || !_videoStarted || _videoSeeking || _parkState != ParkState.None ||
                _videoPlayer.isPaused)
            {
                return;
            }

            double target = VideoTargetFor(time + REPARK_MARGIN_SECONDS * GameManager.SongSpeed);
            if (target < 0 || target >= _videoPlayer.length)
                return;

            _parkState = ParkState.Seeking;
            _releaseParkOnLanding = true;
            SeekVideo(target);
        }

        public void SetSpeed(float speed)
        {
            switch (_type)
            {
                case BackgroundType.Video:
                    if (_source != VenueSource.Song || !_videoPlayer.usingVlc)
                    {
                        _videoPlayer.playbackSpeed = speed;
                        break;
                    }

                    // Never on a playing VLC video: its rate change jumps the picture by (time since
                    // the last seek) x delta-rate, seconds late in a section. A paused, parked or
                    // seeking video takes the new rate at its release or landing; a playing one is
                    // re-parked once the 0.05 steps stop coming.
                    if (_videoStarted && !_videoPlayer.isPaused && _parkState == ParkState.None && !_videoSeeking)
                        _reparkAtUnscaledTime = Time.unscaledTime + SPEED_STEP_SETTLE_SECONDS;
                    break;
            }
        }

        /// <param name="parkAtPlaybackTime">
        /// On pause, the video playback time the resume will rewind to, when it will rewind.
        /// </param>
        public void SetPaused(bool paused, double? parkAtPlaybackTime = null)
        {
            if (paused && parkAtPlaybackTime.HasValue && TryParkForResume(parkAtPlaybackTime.Value))
                return;

            // The park release owns the Play: it waits for the song to reach the parked frame.
            if (!paused && _parkState == ParkState.Releasing)
                return;

            // A held seek that landed while the song was paused (e.g. a section change from the
            // menu): release it rather than play it, so it takes the rate while still paused.
            if (!paused && _parkState == ParkState.Parked && _videoStarted && !_videoSeeking)
            {
                ArmParkRelease();
                return;
            }

            if (paused)
            {
                _parkState = ParkState.None;
                _reparkAtUnscaledTime = float.NaN;
                _releaseParkOnLanding = false;
            }

            // Pause/unpause video
            if (_videoPlayer.playerEnabled && _videoStarted && !_videoSeeking)
            {
                SetVideoPlaying(!paused);

                // The speed changed while paused: re-align at the new rate rather than change it
                // on the playing video.
                if (!paused && _source == VenueSource.Song && _videoPlayer.usingVlc &&
                    !Mathf.Approximately(_videoPlayer.playbackSpeed, GameManager.SongSpeed))
                {
                    _reparkAtUnscaledTime = Time.unscaledTime;
                }
            }

            // The venue is dealt with in the GameManager via Time.timeScale
        }

        private async UniTask<GameObject> GetAddressableCharacter(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                return null;
            }

            const string typeLabel = "character";

            var keys = new[] {typeLabel, hint};

            var validator = Addressables.LoadResourceLocationsAsync(keys, Addressables.MergeMode.Intersection);
            var characterKeys = await validator.Task;

            if (validator.Status == AsyncOperationStatus.Succeeded && characterKeys.Count > 0)
            {
                var filteredKeys = RemoveHiddenCharacters(characterKeys);

                if (filteredKeys.Count == 0)
                {
                    Addressables.Release(validator);
                    return null;
                }

                Addressables.Release(validator);

                var handle = Addressables.LoadAssetAsync<GameObject>(hint);
                _handles.Add(handle);
                return await handle.Task;
            }

            return null;
        }

        private async UniTask<GameObject> GetAddressableCharacter(VocalGender gender)
        {
            const string typeLabel = "character";
            var genderString = gender switch
            {
                VocalGender.Male => "male",
                VocalGender.Female => "female",
                VocalGender.Nonbinary => "nonbinary",
                VocalGender.Other => "other",
                _ => null
            };

            // Unspecified gender means we should fall back to defaults
            if (genderString == null)
            {
                return null;
            }

            var labelGroup = new List<string>() { typeLabel };
            labelGroup.Add(genderString);


            GameObject character = null;
            var locationHandle = Addressables.LoadResourceLocationsAsync(labelGroup, Addressables.MergeMode.Intersection);
            var characterKeys = await locationHandle.Task;

            // If we didn't find a character with the required gender, let the caller deal with it
            if (characterKeys.Count == 0)
            {
                return null;
            }

            if (characterKeys.Count > 0)
            {
                var filteredKeys = RemoveHiddenCharacters(characterKeys);

                var location = filteredKeys[Random.Range(0, filteredKeys.Count)];
                var key = location.PrimaryKey;
                Addressables.Release(locationHandle);
                var handle = Addressables.LoadAssetAsync<GameObject>(key);
                _handles.Add(handle);
                character = await handle.Task;
            }
            else
            {
                YargLogger.LogWarning("No addressable characters with the specified gender exist!");
            }

            return character;
        }

        private static List<IResourceLocation> RemoveHiddenCharacters(IList<IResourceLocation> locations)
        {
            var filteredLocations = locations.ToList();
            for (var i = 0; i < filteredLocations.Count; i++)
            {
                var characterInfo = new CustomCharacterInfo
                {
                    Identifier = filteredLocations[i].PrimaryKey,
                    Source = CustomCharacterSource.Addressable
                };

                if (SettingsManager.Settings.HiddenCharacters.Contains(characterInfo))
                {
                    filteredLocations.RemoveAt(i);
                    i--;
                }
            }

            return filteredLocations;
        }

        private GameObject GetCustomCharacterFromBundle(string characterPath)
        {
            // string characterPath = SettingsManager.Settings.CustomVocalsCharacter.Value;

            if (string.IsNullOrEmpty(characterPath))
            {
                return null;
            }

            var bundle = AssetBundle.LoadFromFile(characterPath);

            if (bundle == null)
            {
                return null;
            }

            _bundleBackgroundManager.CharacterBundles.Add(bundle);

            var character = bundle.LoadAsset<GameObject>(BackgroundHelper.CHARACTER_PREFAB_PATH.ToLowerInvariant());

            // Load Metal shaders
            var shaderBundle = BackgroundHelper.LoadMetalShaders(bundle, character, BackgroundHelper.ExportType.Character);
            if (shaderBundle != null)
            {
                _bundleBackgroundManager.ShaderBundles.Add(shaderBundle);
            }

            return character;
        }

        // gender is a fallback in case the user's specified character fails to load
        private async UniTask<GameObject> GetCustomVocalsCharacter()
        {
            if (SettingsManager.Settings.CustomCharacters.TryGetValue(VenueCharacter.CharacterType.Vocals,
                out var characterInfo))
            {
                if (characterInfo.Source == CustomCharacterSource.None)
                {
                    return null;
                }

                if (characterInfo.Source == CustomCharacterSource.File)
                {
                    return GetCustomCharacterFromBundle(characterInfo.Identifier);
                }

                if (characterInfo.Source == CustomCharacterSource.Addressable)
                {
                    return await GetAddressableCharacter(characterInfo.Identifier);
                }
            }

            return null;
        }

        private async UniTask<bool> LoadCharacter(GameObject venueRoot, string hint, VocalGender gender)
        {
            if (!HasCharacter(venueRoot, VenueCharacter.CharacterType.Vocals))
            {
                return false;
            }

            var character = await GetAddressableCharacter(hint);

            // Hint failed, try user's custom character
            if (character == null)
            {
                character = await GetCustomVocalsCharacter();
            }

            // Couldn't get a custom character, try gender
            if (character == null)
            {
                character = await GetAddressableCharacter(gender);
            }

            var usingCustomChar = LoadCharacter(venueRoot, character);
            return usingCustomChar;
        }

        private static bool HasCharacter(GameObject venueRoot, VenueCharacter.CharacterType type)
        {
            var characters = venueRoot.GetComponentsInChildren<VenueCharacter>();
            foreach (var character in characters)
            {
                if (character.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private bool LoadCharacter(GameObject venueRoot, GameObject character)
        {
            if (character == null)
            {
                YargLogger.LogWarning("Failed to load custom character");
                return false;
            }

            // Load default animation controller and parameters if necessary
            LoadAnimationDefaults(character);

            var venueCharacter = character.GetComponent<VenueCharacter>();
            // Find a character of the same type in venueRoot
            GameObject existingCharacter = null;

            var characters = venueRoot.GetComponentsInChildren<VenueCharacter>();
            foreach (var c in characters)
            {
                if (c.Type == venueCharacter.Type)
                {
                    existingCharacter = c.gameObject;
                    break;
                }
            }

            if (existingCharacter == null)
            {
                YargLogger.LogFormatDebug("Failed to find character of type {0} in venue root", venueCharacter.Type);
                return false;
            }

            // Replace existingCharacter with the new character
            var existingParent = existingCharacter.transform.parent;

            var newCharacter = Instantiate(character, existingParent);
            ReplaceReferences(venueRoot, existingCharacter, newCharacter);
            existingCharacter.SetActive(false);
            Destroy(existingCharacter);

            if (venueCharacter is VRMCharacter vrmCharacter)
            {
                CopyLipsyncToNewCharacter(venueRoot, vrmCharacter);
            }

            AddMicrophoneToCharacter(newCharacter);

            // Lastly, make sure the new character and all its children are in the Venue layer
            var layerIndex = LayerMask.NameToLayer("Venue");
            SetLayer(newCharacter, layerIndex);

            return true;
        }

        private static void CopyLipsyncToNewCharacter(GameObject venueRoot, VRMCharacter character)
        {
            // Find CharacterManager
            var characterManager = venueRoot.GetComponentInChildren<CharacterManager>();

            if (characterManager == null)
            {
                return;
            }

            characterManager.AssignLipsyncToCharacter(character);
        }

        private void AddMicrophoneToCharacter(GameObject character)
        {
            var vrmCharacter = character.GetComponent<VRMCharacter>();
            if (vrmCharacter == null || vrmCharacter.Type != VenueCharacter.CharacterType.Vocals ||
                vrmCharacter.UseCustomAnimations)
            {
                return;
            }

            var animator = vrmCharacter.GetComponent<Animator>();

            if (animator == null)
            {
                return;
            }

            // TODO: Come up with a better means of doing this, because this is both cursed and barely adequate

            // Make sure the animator has taken the character out of t-pose
            animator.Update(0f);

            // Needed to calculate position and rotation
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var rightLittleDistal = animator.GetBoneTransform(HumanBodyBones.RightLittleDistal);
            var rightLittleIntermediate = animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
            var rightLittleProximal = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            var rightRingIntermediate = animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate);
            var rightMiddleIntermediate = animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
            var rightIndexDistal = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
            var rightIndexIntermediate = animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
            var rightIndexProximal = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var rightThumbDistal = animator.GetBoneTransform(HumanBodyBones.RightThumbDistal);

            // Parent microphone to right hand
            var mic = Instantiate(Resources.Load<GameObject>("Animations/Vocals/Microphone"), rightHand);

            var indexGripCenter = Vector3.Lerp(rightThumbDistal.position, rightIndexIntermediate.position, 0.25f);
            var yuiIndexGripCenter = Vector3.Lerp(rightThumbDistal.position, rightIndexProximal.position, 0.25f);
            var littleGripCenter = Vector3.Lerp(rightLittleDistal.position, rightLittleProximal.position, 0.5f);

            mic.transform.position = yuiIndexGripCenter;
        }

        private void LoadAnimationDefaults(GameObject character)
        {
            // Check for an existing animation controller and use default if none is found
            var animator = character.GetComponent<Animator>();
            var vrmCharacter = character.GetComponent<VRMCharacter>();
            if (!vrmCharacter.UseCustomAnimations && animator != null)
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null || !vrmCharacter.UseCustomAnimations)
                {
                    var genre = GetDefaultGenre(GameManager.Song.Genre);
                    var charType = character.GetComponent<VenueCharacter>().Type;
                    var basePath = string.Format(DEFAULT_ANIMATION_CONTROLLER_PATH, charType.ToString(), genre);
                    var controllerPath = Path.Combine(basePath, DEFAULT_ANIMATION_CONTROLLER_FILENAME);
                    var newController = Resources.Load<RuntimeAnimatorController>(controllerPath);
                    if (newController != null)
                    {
                        animator.runtimeAnimatorController = newController;
                        animator.Rebind();

                        // We swapped controllers, so we need to clear the character's animation dicts
                        vrmCharacter.AnimationStates.Clear();
                        vrmCharacter.LayerStates.Clear();
                    }
                    else
                    {
                        YargLogger.LogFormatError("Failed to load default animation controller for {0}", charType);
                    }

                    // Read AnimatorParameters json and set _actionsPerAnimationCycle and _framesToFirstHit
                    var parametersPath = Path.Combine(basePath, DEFAULT_ANIMATION_PARAMETERS_FILENAME);
                    var jsonAsset = Resources.Load<TextAsset>(parametersPath);
                    if (jsonAsset != null)
                    {
                        var props =
                            JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonAsset.text);
                        if (props != null)
                        {
                            vrmCharacter.ActionsPerAnimationCycle = props["ActionsPerAnimationCycle"];
                            vrmCharacter.FramesToFirstHit = props["FramesToFirstHit"];
                        }
                    }
                    else
                    {
                        YargLogger.LogFormatError("Failed to load default animation parameters for {0}", charType);
                    }
                }
            }
            else
            {
                SetCustomGenreSpecificAnimator(vrmCharacter);
            }
        }

        /// <summary>
        /// Checks the supplied VRMCharacter for genre-specific animator overrides
        /// </summary>
        /// <param name="character"></param>
        /// <returns>
        /// true: We found and replaced the character's animator<br />
        /// false: We did not find a genre-specific animator override
        /// </returns>
        private bool SetCustomGenreSpecificAnimator(VRMCharacter character)
        {
            if (character == null)
            {
                return false;
            }

            var animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                return false;
            }

            var anims = character.GetGenreSpecificAnimations();

            if (anims == null || anims.Keys.Count <= 0)
            {
                return false;
            }

            var animationGenres = new List<Genrelizer.BaseGenre>();

            if (GameManager.Chart.AnimationGenre is not MiloAnimation.MiloAnimationGenre.None)
            {
                animationGenres.AddRange(Genrelizer.GetAnimationGenres(GameManager.Chart.AnimationGenre));
            }
            // Add the song's actual base genre at the end of the list, if we can't find any from the animation genre
            animationGenres.Add(Genrelizer.GetBaseGenre(GameManager.Song.Genre));

            Genrelizer.BaseGenre? genre = null;
            foreach (var animationGenre in animationGenres)
            {
                if (anims.ContainsKey(animationGenre))
                {
                    genre = animationGenre;
                    break;
                }
            }

            if (genre is null)
            {
                return false;
            }

            var controller = anims[genre.Value];
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            return true;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        // It would be better if we could replace all references, but I'm not sure how to do that, so I'm fixing up the ones I know how to do
        public void ReplaceReferences(GameObject venueRoot, GameObject oldObject, GameObject newObject)
        {
            Transform hips = null;
            Transform head = null;
            var humanoid = newObject.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                hips = humanoid.Hips;
                head = humanoid.Head;
            }

            // Find references to oldObject.transform anywhere in venueRoot..for now we'll just deal with Cinemachine and Lights having lookat/follow properties
            var lookAts = venueRoot.GetComponentsInChildren<LookAtConstraint>(true);
            var sources = new List<ConstraintSource>();
            foreach (var lookat in lookAts)
            {
                sources.Clear();
                lookat.GetSources(sources);

                for (int i = 0; i < sources.Count; i++)
                {
                    var s = sources[i];
                    if (s.sourceTransform != null && s.sourceTransform.IsChildOf(oldObject.transform))
                    {
                        if (head != null && (s.sourceTransform.gameObject.name.Contains("Head") ||
                            s.sourceTransform.gameObject.name.Contains("Face")))
                        {
                            s.sourceTransform = head;
                        }
                        else if (hips != null && s.sourceTransform.gameObject.name.Contains("Hips"))
                        {
                            s.sourceTransform = hips;
                        }
                        else
                        {
                            s.sourceTransform = newObject.transform;
                        }

                        sources[i] = s;
                    }
                }

                lookat.SetSources(sources);
            }

            var cinemachines = venueRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true);
            foreach (var cinemachine in cinemachines)
            {
                // If we can easily determine face/hips, we use the corresponding transform on the VRM character, otherwise we default to hips if set, otherwise newObject.transform
                // We also use a heuristic based on the camera name so as to make certain existing venues not look stupid on the Vocals Closeup cam
                var follow = cinemachine.Follow;
                if (follow != null && follow.IsChildOf(oldObject.transform))
                {
                    if (head != null &&
                        (follow.gameObject.name.Contains("Face") ||
                         follow.gameObject.name.Contains("Head") ||
                         cinemachine.gameObject.name == "Vocals Closeup" ||
                         cinemachine.gameObject.name.EndsWith("Closeup Head")))
                    {
                        cinemachine.Follow = head;
                    }
                    else if (hips != null)
                    {
                        cinemachine.Follow = hips;
                    }
                    else
                    {
                        cinemachine.Follow = newObject.transform;
                    }
                }

                var lookAt = cinemachine.LookAt;
                if (lookAt != null && lookAt.IsChildOf(oldObject.transform))
                {
                    if (head != null && (lookAt.gameObject.name.Contains("Face") ||
                        lookAt.gameObject.name.Contains("Head") ||
                        cinemachine.gameObject.name == "Vocals Closeup" ||
                        cinemachine.gameObject.name.EndsWith("Closeup Head")))
                    {
                        cinemachine.LookAt = head;
                    }
                    else if (hips != null)
                    {
                        cinemachine.LookAt = hips;
                    }
                    else
                    {
                        cinemachine.LookAt = newObject.transform;
                    }
                }
            }
        }

        private void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }

        private void SetYargroundOrigin(GameObject venueRoot)
        {
            // Calculate bounds for everything in venueRoot
            venueRoot.transform.localPosition = Vector3.zero;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var children = venueRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var child in children)
            {
                bounds.Encapsulate(child.bounds);
            }

            var sizeX = bounds.size.x;
            var sizeZ = bounds.size.z;

            var offsetX = (sizeX * 0.5f) + YARGROUND_OFFSET;
            var offsetZ = (sizeZ * 0.5f) + YARGROUND_OFFSET;

            // New origin places maxZ and maxX at -50
            venueRoot.transform.position = new Vector3(-offsetX, 0, -offsetZ);
        }

        // TODO: Move this to Genrelizer or sth and implement
        public static string GetDefaultGenre(string realGenre)
        {
            return "Default";
        }

        public void Dispose()
        {
            if (VIDEO_PATH != null)
            {
                File.Delete(VIDEO_PATH);
                VIDEO_PATH = null;
            }

            // In case this somehow doesn't happen in GameplayDestroy
            if (loadedAddressable)
            {
                foreach (var handle in _handles)
                {
                    Addressables.Release(handle);
                }
                loadedAddressable = false;
                _handles.Clear();
            }

#if UNITY_EDITOR
            if (_usingEditorVenue)
            {
                SceneManager.UnloadSceneAsync(_editorVenueScene);
            }
#endif
        }

        protected override void GameplayDestroy()
        {
            if (loadedAddressable)
            {
                foreach (var handle in _handles)
                {
                    Addressables.Release(handle);
                }
                loadedAddressable = false;
                _handles.Clear();
            }
        }

        ~BackgroundManager()
        {
            Dispose();
        }
    }
}
