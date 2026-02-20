using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UniHumanoid;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.Venue;
using YARG.Helpers.Extensions;
using YARG.Settings;
using YARG.Venue;
using YARG.Venue.Characters;
using YARG.Core.Logging;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
using System.Collections.Generic;
#endif

namespace YARG.Gameplay
{
    public class BackgroundManager : GameplayBehaviour, IDisposable
    {
        // e.g. DefaultController.Vocals.Rock.controller
        private const string DEFAULT_ANIMATION_CONTROLLER_PATH = "DefaultAnimations/DefaultController.{0}.{1}.controller";

        private string VIDEO_PATH;

        [SerializeField]
        private VideoPlayer _videoPlayer;

        [SerializeField]
        private RawImage _backgroundImage;

        [SerializeField]
        private Image _backgroundDimmer;

        [SerializeField]
        private RawImage _venueOutput;

        private BackgroundType _type;
        private VenueSource _source;

        private bool _videoStarted = false;
        private bool _videoSeeking = false;

        // These values are relative to the video, not to song time!
        // A negative start time will delay when the video starts, a positive one will set the video position
        // to that value when starting playback at the start of a song.
        private double _videoStartTime;
        // End time cannot be negative; a negative value means it is not set.
        private double _videoEndTime;

        private AssetBundle _characterBundle;

#if UNITY_EDITOR
        private bool        _usingEditorVenue;
        private string      _editorVenuePath;
        private Scene       _editorVenueScene;
#endif
        // "The Unity message 'Start' has an incorrect signature."
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

                _venueOutput.gameObject.SetActive(true);

                var editorRenderers = editorBg.GetComponentsInChildren<Renderer>(true);

                // Song specific textures
                var tm = GetComponent<TextureManager>();
                var songBg = GameManager.Song.LoadBackground();

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
                return;
            }
#endif

            using var result = VenueLoader.GetVenue(GameManager.Song, out _source);
            if (result == null)
            {
                return;
            }

            var colorDim = _backgroundDimmer.color.WithAlpha(1 - SettingsManager.Settings.SongBackgroundOpacity.Value);

            _backgroundDimmer.color = colorDim;

            _type = result.Type;
            switch (_type)
            {
                case BackgroundType.Yarground:
                    LoadYarground(result);
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

        private async UniTaskVoid LoadYarground(BackgroundResult result)
        {
            var bundle = AssetBundle.LoadFromStream(result.Stream);
            AssetBundle shaderBundle = null;

            _venueOutput.gameObject.SetActive(true);
            // KEEP THIS PATH LOWERCASE
            // Breaks things for other platforms, because Unity
            var bg = (GameObject) await bundle.LoadAssetAsync<GameObject>(
                BundleBackgroundManager.BACKGROUND_PREFAB_PATH.ToLowerInvariant());
            var renderers = bg.GetComponentsInChildren<Renderer>(true);

            // Load Metal shaders, if necessary
            shaderBundle = await LoadMetalShaders(bundle, bg);

            // Hookup song-specific textures
            var textureManager = GetComponent<TextureManager>();
            // Load SongBackground here to determine if textures need to be replaced
            var songBackground = GameManager.Song.LoadBackground();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    textureManager.ProcessMaterial(material, songBackground?.Type);
                }
            }

            var bgInstance = Instantiate(bg);
            var bundleBackgroundManager = bgInstance.GetComponent<BundleBackgroundManager>();
            bundleBackgroundManager.Bundle = bundle;
            bundleBackgroundManager.ShaderBundle = shaderBundle;
            bundleBackgroundManager.SetupVenueCamera(bgInstance);
            bundleBackgroundManager.LimitVenueLights(bgInstance);

            // Destroy the default camera (venue has its own)
            Destroy(_videoPlayer.targetCamera.gameObject);

            if (textureManager.VideoTexFound())
            {
                SetUpVideoTexture(songBackground);
            }

            LoadCustomCharacter(bgInstance);

            // Initialize CharacterManager, if it exists
            var characterManager = bgInstance.GetComponentInChildren<CharacterManager>();
            if (characterManager != null)
            {
                characterManager.Initialize();
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
                    _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
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

        private void LoadVideoBackground(BackgroundResult bg)
        {
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

            _videoPlayer.enabled = true;
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.seekCompleted += OnVideoSeeked;
            _videoPlayer.Prepare();
            enabled = true;
        }

        private void Update()
        {
            if (_videoSeeking)
                return;

            double time = GameManager.SongTime + GameManager.Song.SongOffsetSeconds;
            // Start video
            if (!_videoStarted)
            {
                // Don't start playing the video until the start of the song
                if (time < 0.0)
                    return;

                // Delay until the start time is reached
                if (_source == VenueSource.Song && time < -_videoStartTime)
                    return;

                if (_videoEndTime == 0)
                    return;

                _videoStarted = true;
                _videoPlayer.Play();

                // Disable after starting the video if it's not from the song folder
                // or if video end time is not specified
                if (_source != VenueSource.Song || double.IsNaN(_videoEndTime))
                {
                    enabled = false;
                    return;
                }
            }

            // End video when reaching the specified end time
            if (time + _videoStartTime >= _videoEndTime)
            {
                _videoPlayer.Stop();
                _videoPlayer.enabled = false;
                enabled = false;
            }
        }

        // Some video player properties don't work correctly until
        // it's finished preparing, such as the length
        private void OnVideoPrepared(VideoPlayer player)
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

                player.time = _videoStartTime;
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
                        _videoEndTime = player.length;
                    }
                }
            }
            else
            {
                _videoStartTime = 0;
                _videoEndTime = double.NaN;
                player.isLooping = true;
            }
        }

        public void SetTime(double songTime)
        {
            switch (_type)
            {
                case BackgroundType.Video:
                    // Don't seek videos that aren't from the song
                    if (_source != VenueSource.Song)
                        return;

                    double videoTime = songTime + _videoStartTime;
                    if (videoTime < 0f) // Seeking before video start
                    {
                        enabled = true;
                        _videoPlayer.enabled = true;
                        _videoStarted = false;
                        _videoPlayer.Stop();
                    }
                    else if (videoTime >= _videoPlayer.length) // Seeking after video end
                    {
                        enabled = false;
                        _videoPlayer.enabled = false;
                        _videoPlayer.Stop();
                    }
                    else
                    {
                        enabled = false; // Temp disable
                        _videoPlayer.enabled = true;

                        // Hack to ensure the video stays synced to the audio
                        _videoSeeking = true; // Signaling flag; must come first
                        if (SettingsManager.Settings.WaitForSongVideo.Value)
                            GameManager.OverridePause();

                        _videoPlayer.time = videoTime;
                    }
                    break;
            }
        }

        private void OnVideoSeeked(VideoPlayer player)
        {
            if (!_videoSeeking)
                return;

            if (!SettingsManager.Settings.WaitForSongVideo.Value || GameManager.OverrideResume())
                player.Play();

            enabled = !double.IsNaN(_videoEndTime);
            _videoSeeking = false;
        }

        public void SetSpeed(float speed)
        {
            switch (_type)
            {
                case BackgroundType.Video:
                    _videoPlayer.playbackSpeed = speed;
                    break;
            }
        }

        public void SetPaused(bool paused)
        {
            // Pause/unpause video
            if (_videoPlayer.enabled && _videoStarted && !_videoSeeking)
            {
                if (paused)
                {
                    _videoPlayer.Pause();
                }
                else
                {
                    _videoPlayer.Play();
                }
            }

            // The venue is dealt with in the GameManager via Time.timeScale
        }

        private void LoadCustomCharacter(GameObject venueRoot)
        {
            string characterPath = SettingsManager.Settings.CustomVocalsCharacter.Value;

            if (string.IsNullOrEmpty(characterPath))
            {
                return;
            }

            var bundle = AssetBundle.LoadFromFile(characterPath);

            if (bundle == null)
            {
                return;
            }

            _characterBundle = bundle;

            var character = bundle.LoadAsset<GameObject>(BundleBackgroundManager.CHARACTER_PREFAB_PATH.ToLowerInvariant());
            if (character == null)
            {
                YargLogger.LogFormatError("Failed to load character from {0}", characterPath);
                return;
            }

            // Check for an existing animation controller and use default if none is found
            var animator = character.GetComponent<Animator>();
            if (animator != null)
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null)
                {
                    var genre = GetDefaultGenre(GameManager.Song.Genre);
                    var charType = character.GetComponent<VenueCharacter>().Type;
                    var path = string.Format(DEFAULT_ANIMATION_CONTROLLER_PATH, charType.ToString(), genre);
                    var newController = Resources.Load<RuntimeAnimatorController>(path);
                    if (newController != null)
                    {
                        animator.runtimeAnimatorController = newController;
                    }
                    else
                    {
                        YargLogger.LogFormatError("Failed to load default animation controller for {0}", charType);
                    }
                }
            }

            var newType = character.GetComponent<VenueCharacter>().Type;
            // Find a character of the same type in venueRoot
            GameObject existingCharacter = null;

            var characters = venueRoot.GetComponentsInChildren<VenueCharacter>();
            foreach (var c in characters)
            {
                if (c.Type == newType)
                {
                    existingCharacter = c.gameObject;
                    break;
                }
            }

            if (existingCharacter == null)
            {
                YargLogger.LogFormatError("Failed to find character of type {0} in venue root", newType);
                return;
            }

            // Replace existingCharacter with the new character
            var existingParent = existingCharacter.transform.parent;

            var newCharacter = Instantiate(character, existingParent);
            ReplaceReferences(venueRoot, existingCharacter, newCharacter);
            existingCharacter.SetActive(false);
            Destroy(existingCharacter);

            // Lastly, make sure the new character and all its children are in the Venue layer
            var layerIndex = LayerMask.NameToLayer("Venue");
            SetLayer(newCharacter, layerIndex);
        }

        private async UniTask<AssetBundle> LoadMetalShaders(AssetBundle bundle, GameObject bg)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            AssetBundle shaderBundle = null;
            var renderers = bg.GetComponentsInChildren<Renderer>(true);
            var metalShaders = new Dictionary<string, Shader>();

            var shaderBundleData = (TextAsset)await bundle.LoadAssetAsync<TextAsset>(
                "Assets/" + BundleBackgroundManager.BACKGROUND_SHADER_BUNDLE_NAME
            );

            if (shaderBundleData != null && shaderBundleData.bytes.Length > 0)
            {
                YargLogger.LogInfo("Loading Metal shader bundle");
                shaderBundle = await AssetBundle.LoadFromMemoryAsync(shaderBundleData.bytes);
                var allAssets = shaderBundle.LoadAllAssets<Shader>();
                foreach (var shader in allAssets)
                {
                    metalShaders.Add(shader.name, shader);
                }
            }
            else
            {
                YargLogger.LogInfo("Did not find Metal shader bundle");
            }

            // Yarground comes with shaders for dx11/dx12/glcore/vulkan
            // Metal shaders used on OSX come in this separate bundle
            // Update our renderers to use them

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    var shaderName = material.shader.name;
                    if (metalShaders.TryGetValue(shaderName, out var shader))
                    {
                        YargLogger.LogFormatDebug("Found bundled shader {0}", shaderName);
                        // We found shader from Yarground
                        material.shader = shader;
                    }
                    else
                    {
                        YargLogger.LogFormatDebug("Did not find bundled shader {0}", shaderName);
                        // Fallback to try to find among builtin shaders
                        material.shader = Shader.Find(shaderName);
                    }
                }
            }

            return shaderBundle;
#endif
            // Fallback if we're not running on OSX
            return null;
        }

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

        // TODO: Move this to Genrelizer or sth and implement
        public static string GetDefaultGenre(string realGenre)
        {
            return "Generic";
        }

        public void Dispose()
        {
            if (VIDEO_PATH != null)
            {
                File.Delete(VIDEO_PATH);
                VIDEO_PATH = null;
            }

            if (_characterBundle != null)
            {
                _characterBundle.Unload(true);
                _characterBundle = null;
            }
#if UNITY_EDITOR
            if (_usingEditorVenue)
            {
                SceneManager.UnloadSceneAsync(_editorVenueScene);
            }
#endif
        }

        ~BackgroundManager()
        {
            Dispose();
        }
    }
}
