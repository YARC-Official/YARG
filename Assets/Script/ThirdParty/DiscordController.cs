using System;
using Discord;
using UnityEngine;
using YARG.PlayMode;
using YARG.Song;
using YARG.UI;

namespace YARG.ThirdParty
{
    public class DiscordController : MonoBehaviour
    {
        /*
        TODO:
        Determine if discord is installed at all on windows, linux and mac. Don't bother running otherwise (doubt anyone would install it DURING gameplay lol)
        Reconnect if discord is closed and reopened/opened during game (and not memory leak the game into a crash)

        Unsure if possible:
        If Yarg crashes the discord presences will stay

        Impossible at the moment:
        Display Album art with little icon overlay - Currently only possible with the api's art assets or a url, NOT with a local image file
        Progress bar
        Pausing the timer (instead of blanking it)
        Other specalized things that only very popular apps can do (like spotify and fortnite)
        */

        public static DiscordController Instance { get; private set; }

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
            Instance = this;

            // if it's a Nightly build, use the Nightly logo, otherwise use the Stable logo
            if (Constants.VERSION_TAG.beta)
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

            // Listen to the changing of songs
            Play.OnSongStart += OnSongStart;
            Play.OnSongEnd += OnSongEnd;

            // Listen to instrument selection
            DifficultySelect.OnInstrumentSelection += OnInstrumentSelection;

            // Listen to pausing
            Play.OnPauseToggle += OnPauseToggle;

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

        private void OnDestroy()
        {
            // Not sure why the discord controller would ever be destroyed but, eh you never know, right?
            Play.OnSongStart -= OnSongStart;
            Play.OnSongEnd -= OnSongEnd;
            DifficultySelect.OnInstrumentSelection -= OnInstrumentSelection;
            Play.OnPauseToggle -= OnPauseToggle;
        }

        private void OnPauseToggle(bool pause)
        {
            SetActivity(
                // State data
                pause ? "pause1" : _currentSmallImage,
                pause ? "Paused" : _currentSmallText,

                // Song data
                _songName,
                "by " + _artistName,

                // Time data
                pause ? 0 : DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                pause
                    ? 0
                    : DateTimeOffset.Now.AddSeconds((Play.Instance.SongLength - Play.Instance.SongTime) / Play.speed)
                        .ToUnixTimeMilliseconds()
            );
        }

        private void OnSongStart(SongEntry song)
        {
            _songName = song.Name;
            if (Play.speed != 1f)
            {
                _songName += $" ({Play.speed * 100f}%)";
            }

            _artistName = song.Artist;

            // if more then 1 player is playing, set the source icon
            if (PlayerManager.players.Count > 1)
            {
                SetSourceIcon();
            }

            SetActivity(
                _currentSmallImage,
                _currentSmallText,
                _songName,
                "by " + _artistName,
                DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                DateTimeOffset.Now.AddSeconds((Play.Instance.SongLength - Play.Instance.SongTime) / Play.speed)
                    .ToUnixTimeMilliseconds()
            );
        }

        private void OnSongEnd(SongEntry song)
        {
            SetDefaultActivity();
        }

        private void OnInstrumentSelection(PlayerManager.Player playerInfo)
        {
            // ToLowerInvariant() because the DISCORD API DOESN'T HAVE UPPERCASE ARTWORK NAMES (WHY)
            _currentSmallImage = playerInfo.chosenInstrument.ToLowerInvariant();

#pragma warning disable format

            _currentSmallText = playerInfo.chosenInstrument switch
            {
                "vocals"     => "Belting one out",
                "harmVocals" => "Belting one out, with friends!",
                "drums"      => "Working the skins",
                "realDrums"  => "Really working the skins",
                "ghDrums"    => "Working the skins +1",
                "guitar"     => "Making it talk",
                "guitarCoop" => "GTR_COOP_PLACEHOLDER",
                "rhythm"     => "RHYTHM_PLACEHOLDER",
                "realGuitar" => "Really making it talk",
                "bass"       => "In the groove",
                "realBass"   => "Really in the groove",
                "keys"       => "Tickling the ivory",
                "realKeys"   => "Really tickling the ivory",
                _            => ""
            };

#pragma warning restore format
        }

        private void SetSourceIcon()
        {
            var sourceIconName = SongSources.GetSource(Play.Instance.Song.Source);

            _currentSmallImage = sourceIconName.GetIconURL();
            _currentSmallText = sourceIconName.GetDisplayName();
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
                Debug.LogException(e);

                TryDispose();
            }
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
                Debug.Log("Failed to clear activity or dispose of Discord.");
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