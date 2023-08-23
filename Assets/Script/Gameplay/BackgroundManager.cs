using UnityEngine;
using UnityEngine.Video;
using YARG.Venue;

namespace YARG.Gameplay
{
    public class BackgroundManager : GameplayBehaviour
    {
        [SerializeField]
        private VideoPlayer _videoPlayer;

        private bool _videoStarted;

        private void Start()
        {
            LoadBackground();
        }

        private void LoadBackground()
        {
            var typePathPair = VenueLoader.GetVenuePath(GameManager.Song);
            if (typePathPair == null)
            {
                return;
            }

            var type = typePathPair.Value.Type;
            var path = typePathPair.Value.Path;

            switch (type)
            {
                case VenueType.Yarground:
                    var bundle = AssetBundle.LoadFromFile(path);

                    // KEEP THIS PATH LOWERCASE
                    // Breaks things for other platforms, because Unity
                    var bg = bundle.LoadAsset<GameObject>(BundleBackgroundManager.BACKGROUND_PREFAB_PATH
                        .ToLowerInvariant());

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
                    break;
                case VenueType.Image:
                    // var png = ImageHelper.LoadTextureFromFile(path);
                    // GameUI.Instance.background.texture = png;
                    break;
            }
        }

        private void Update()
        {
            // Start playing the video at 0 seconds
            if (!_videoStarted && GameManager.SongTime >= 0.0)
            {
                _videoStarted = true;
                _videoPlayer.Play();
            }
        }
    }
}