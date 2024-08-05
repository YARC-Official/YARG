using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Venue;
using YARG.Helpers.Extensions;
using YARG.Settings;
using YARG.Venue;

namespace YARG.Gameplay
{
    public class BackgroundManager : GameplayBehaviour, IDisposable
    {
        private string VIDEO_PATH;
        [SerializeField]
        private VideoPlayer _videoPlayer;
        [SerializeField]
        private RawImage _backgroundImage;

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

        // "The Unity message 'Start' has an incorrect signature."
        [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
        private async UniTaskVoid Start()
        {
            // We don't need to update unless we're using a video
            enabled = false;

            using var result = VenueLoader.GetVenue(GameManager.Song, out _source);
            if (result == null)
            {
                return;
            }

            _type = result.Type;
            switch (_type)
            {
                case BackgroundType.Yarground:
                    var bundle = AssetBundle.LoadFromStream(result.Stream);

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
                case BackgroundType.Video:
                    switch (result.Stream)
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
                            result.Stream.CopyTo(tmp);
                            _videoPlayer.url = VIDEO_PATH;
                            break;
                        }
                    }

                    _videoPlayer.enabled = true;
                    _videoPlayer.prepareCompleted += OnVideoPrepared;
                    _videoPlayer.seekCompleted += OnVideoSeeked;
                    _videoPlayer.Prepare();
                    enabled = true;
                    break;
                case BackgroundType.Image:
                    _backgroundImage.texture = result.Image.LoadTexture(false);
                    _backgroundImage.uvRect = new Rect(0f, 0f, 1f, -1f);
                    _backgroundImage.gameObject.SetActive(true);
                    break;
            }
        }

        private void Update()
        {
            if (_videoSeeking)
                return;

            // Start video
            if (!_videoStarted)
            {
                // Don't start playing the video until the start of the song
                if (GameManager.SongTime < 0.0)
                    return;

                // Delay until the start time is reached
                if (_source == VenueSource.Song &&
                    _videoStartTime < 0 && GameManager.SongTime < -_videoStartTime)
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
            // Start time is considered set if it is greater than 25 ms in either direction
            // End time is only set if it is greater than 0
            // Video will only loop if its length is less than 85% of the song's length
            const double startTimeThreshold = 0.025;
            const double endTimeThreshold = 0;
            const double dontLoopThreshold = 0.85;

            if (_source == VenueSource.Song)
            {
                _videoStartTime = GameManager.Song.VideoStartTimeSeconds;
                _videoEndTime = GameManager.Song.VideoEndTimeSeconds;
                if (_videoEndTime <= 0)
                    _videoEndTime = double.NaN;

                player.time = _videoStartTime;
                player.playbackSpeed = GameManager.SongSpeed;

                // Determine whether or not to loop the video
                if (Math.Abs(_videoStartTime) <= startTimeThreshold && _videoEndTime <= endTimeThreshold)
                {
                    // Only loop the video if it's not around the same length as the song
                    double lengthRatio = player.length / GameManager.SongLength;
                    player.isLooping = lengthRatio < dontLoopThreshold;
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

            enabled = double.IsNaN(_videoEndTime);
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

        public void Dispose()
        {
            if (VIDEO_PATH != null)
            {
                File.Delete(VIDEO_PATH);
                VIDEO_PATH = null;
            }
        }

        ~BackgroundManager()
        {
            Dispose();
        }
    }
}