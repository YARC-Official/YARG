using System;
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

        private VenueInfo _venueInfo;

        private bool _videoStarted = false;

        // These values are relative to the video, not to song time!
        // A negative start time will delay when the video starts, a positive one will set the video position
        // to that value when starting playback at the start of a song.
        private double _videoStartTime;
        // End time cannot be negative; a negative value means it is not set.
        private double _videoEndTime;

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            // We don't need to update unless we're using a video
            enabled = false;

            var venueInfo = VenueLoader.GetVenue(GameManager.Song);
            if (!venueInfo.HasValue)
            {
                return;
            }

            _venueInfo = venueInfo.Value;

            var type = _venueInfo.Type;
            var path = _venueInfo.Path;

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
                    _videoPlayer.prepareCompleted += OnVideoPrepared;
                    _videoPlayer.Prepare();

                    enabled = true;
                    break;
                case VenueType.Image:
                    _backgroundImage.gameObject.SetActive(true);
                    _backgroundImage.texture = await TextureHelper.Load(path);
                    break;
            }
        }

        private void Update()
        {
            // Start video
            if (!_videoStarted)
            {
                // Don't start playing the video until the start of the song
                if (GameManager.SongTime < 0.0)
                    return;

                // Delay until the start time is reached
                if (_venueInfo.Source == VenueSource.Song &&
                    _videoStartTime < 0 && GameManager.SongTime < -_videoStartTime)
                    return;

                _videoStarted = true;
                _videoPlayer.Play();

                // Disable after starting the video if it's not from the song folder
                // or if video end time is not specified
                if (_venueInfo.Source != VenueSource.Song || _videoEndTime <= 0)
                {
                    enabled = false;
                    return;
                }
            }

            // End video when reaching the specified end time
            if (GameManager.SongTime - _videoStartTime >= _videoEndTime)
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
            if (_venueInfo.Source == VenueSource.Song)
            {
                _videoStartTime = GameManager.Song.VideoStartTimeSeconds;
                _videoEndTime = GameManager.Song.VideoEndTimeSeconds;

                player.time = _videoStartTime;

                // Determine whether or not to loop the video
                double frameTime = 1 / player.frameRate;
                if (Math.Abs(_videoStartTime) < frameTime && _videoEndTime < 0)
                {
                    // Only loop the video if it's not around the same length as the song
                    double lengthRatio = (player.length - _videoStartTime) / GameManager.SongLength;
                    player.isLooping = lengthRatio < 0.85;
                }
                else
                {
                    // Never loop the video if start/end times are specified
                    player.isLooping = false;
                }
            }
            else
            {
                _videoStartTime = 0;
                _videoEndTime = -1;
                player.isLooping = true;
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