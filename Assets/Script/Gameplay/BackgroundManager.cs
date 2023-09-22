using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.Helpers;
using YARG.Venue;

namespace YARG.Gameplay
{
    public class BackgroundManager : GameplayBehaviour
    {
        [SerializeField]
        private VideoPlayer _videoPlayer;
        [SerializeField]
        private RawImage _backgroundImage;

        private bool _videoShouldBeStarted;

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTask is a compatible return type.")]
        private async UniTask Start()
        {
            var typePathPair = VenueLoader.GetVenuePath(GameManager.Song);
            if (typePathPair == null)
            {
                return;
            }

            var type = typePathPair.Value.Type;
            var path = typePathPair.Value.Path;

            // Set to false (unless we do wanna start the video)
            _videoShouldBeStarted = false;

            switch (type)
            {
                case VenueType.Yarground:
                    var bundle = AssetBundle.LoadFromFile(path);

                    // KEEP THIS PATH LOWERCASE
                    // Breaks things for other platforms, because Unity
                    var bg = (GameObject) await bundle.LoadAssetAsync<GameObject>(
                        BundleBackgroundManager.BACKGROUND_PREFAB_PATH.ToLowerInvariant());

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                    // Fix for non-Windows machines
                    // Probably there's a better way to do this.
					Renderer[] renderers = bg.GetComponentsInChildren<Renderer>();

					foreach (Renderer renderer in renderers) {
						Material[] materials = renderer.sharedMaterials;

						for (int i = 0; i < materials.Length; i++) {
							Material material = materials[i];
							material.shader = Shader.Find(material.shader.name);
						}
					}
#endif

                    var bgInstance = Instantiate(bg);

                    bgInstance.GetComponent<BundleBackgroundManager>().Bundle = bundle;
                    break;
                case VenueType.Video:
                    _videoPlayer.url = path;
                    _videoPlayer.enabled = true;
                    _videoPlayer.Prepare();

                    _videoShouldBeStarted = true;
                    break;
                case VenueType.Image:
                    _backgroundImage.gameObject.SetActive(true);
                    _backgroundImage.texture = await TextureHelper.Load(path);
                    break;
            }
        }

        private void Update()
        {
            // Start playing the video at 0 seconds
            if (_videoShouldBeStarted && GameManager.SongTime >= 0.0)
            {
                _videoShouldBeStarted = false;
                _videoPlayer.Play();
            }
        }

        public void SetPaused(bool paused)
        {
            // Pause/unpause video
            if (_videoPlayer.enabled)
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
    }
}