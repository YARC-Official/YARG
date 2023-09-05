using System;
using Discord;
using UnityEngine;
using YARG.Core.Song;
using YARG.PlayMode;
using YARG.Song;

namespace YARG.Integration
{
    // TODO: Fix this

    public class DiscordController : MonoSingleton<DiscordController>
    {
        private Discord.Discord _discord;

        public Activity CurrentActivity
        {
            set
            {
                _discord?.GetActivityManager().UpdateActivity(value, result =>
                {
                    if (result != Result.Ok)
                    {
                        Debug.LogWarning("Discord Activity Error: " + result);
                    }
                });
            }
        }

        [Serializable]
        private struct DistinctDetails
        {
            public string DefaultDetails;
            public string DefaultState;
            public string DefaultLargeImage;
            public string DefaultLargeText;
        }

        private DistinctDetails _defaultDetails;

        [SerializeField]
        private long _applicationID = 1091177744416637028;

        [Space]
        [Header("Default Values")]
        [SerializeField]
        private DistinctDetails _stableDetails = new()
        {
            DefaultDetails = "Hello there ladies and gentlemen!",
            DefaultState = "Are you ready to rock?",
            DefaultLargeImage = "icon_stable",
            DefaultLargeText = "Yet Another Rhythm Game"
        };

        [SerializeField]
        private DistinctDetails _nightlyDetails = new()
        {
            DefaultDetails = "Hello there ladies and gentlemen!",
            DefaultState = "Are you ready to test?",
            DefaultLargeImage = "icon_nightly",
            DefaultLargeText = "Yet Another Rhythm Game - Nightly Build"
        };

        [SerializeField]
        private DistinctDetails _devDetails = new()
        {
            DefaultDetails = "Hello there ladies and gentlemen!",
            DefaultState = "Are you ready to develop?",
            DefaultLargeImage = "icon_dev",
            DefaultLargeText = "Yet Another Rhythm Game - Developer Build"
        };

        // string name comes from the assets uploaded to the app
        private string _currentLargeImage;

        // little overlay on the bottom right of the small image
        private string _currentSmallImage;

        // Tooltip for smallImage
        private string _currentSmallText;
        private string _songName;
        private string _artistName;

        // Start of YARG, doesn't stop
        private long _gameStartTime;

        private void Start()
        {
            // if it's a Nightly build, use the Nightly logo, otherwise use the Stable logo
            if (GlobalVariables.CurrentVersion.IsPrerelease)
            {
                _defaultDetails = _nightlyDetails;
            }
            else
            {
                _defaultDetails = _stableDetails;
            }

            // if it's running in the editor, use the Dev logo
#if UNITY_EDITOR
            _defaultDetails = _devDetails;
#endif
            
            // Listen to the changing of states
            GameStateFetcher.GameStateChange += OnGameStateChange;
            
            // Create the Discord instance
            try
            {
                _discord = new Discord.Discord(_applicationID, (ulong) CreateFlags.NoRequireDiscord);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to start Discord presence client.");
                Debug.LogException(e);

                _discord = null;
                return;
            }

            // Start the activity
            _gameStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Set default activity
            SetDefaultActivity();
        }

        private void OnGameStateChange(GameStateFetcher.State state)
        {

        }

        private void Update()
        {
            if (_discord == null)
            {
                return;
            }

            try
            {
                _discord.RunCallbacks();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to run Discord client callbacks!");
                Debug.LogException(e);

                TryDispose();
            }
        }

        private void OnDestroy() {
            GameStateFetcher.GameStateChange -= OnGameStateChange;
        }

        private void TryDispose()
        {
            if (_discord == null)
            {
                return;
            }

            try
            {
                _discord.GetActivityManager().ClearActivity(_ => { });
                _discord.Dispose();
                _discord = null;
            }
            catch (Exception e)
            {
                Debug.Log("Failed to dispose of Discord client.");
                Debug.LogException(e);
            }
        }

        private void OnApplicationQuit()
        {
            TryDispose();
        }

        private void SetDefaultActivity()
        {
            CurrentActivity = new Activity
            {
                Assets =
                {
                    LargeImage = _defaultDetails.DefaultLargeImage,
                    LargeText = _defaultDetails.DefaultLargeText,
                    SmallImage = string.Empty,
                    SmallText = string.Empty
                },
                Details = _defaultDetails.DefaultDetails,
                State = _defaultDetails.DefaultState,
                Timestamps =
                {
                    Start = _gameStartTime
                }
            };
        }

        private void SetActivity(string smallImage, string smallText, string details, string state, long startTimeStamp,
            long endTimeStamp)
        {
            CurrentActivity = new Activity
            {
                Assets =
                {
                    LargeImage =
                        _defaultDetails
                            .DefaultLargeImage, //the YARG logo and tooltip does not change, at this point in time.
                    LargeText = _defaultDetails.DefaultLargeText,
                    SmallImage = smallImage,
                    SmallText = smallText,
                },
                Details = details,
                State = state,
                Timestamps =
                {
                    Start = startTimeStamp, End = endTimeStamp,
                }
            };
        }
    }
}