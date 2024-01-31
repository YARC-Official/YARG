using System;
using Discord;
using UnityEngine;
using UnityEngine.Localization;
using YARG.Helpers;

namespace YARG.Integration
{
    public class DiscordController : MonoSingleton<DiscordController>
    {
        private const long APPLICATION_ID = 1091177744416637028;

#if UNITY_EDITOR || YARG_TEST_BUILD
        private const string LARGE_TEXT_KEY = "Discord.Default.LargeText.Dev";
        private const string LARGE_ICON_KEY = "icon_dev";
#elif YARG_NIGHTLY_BUILD
        private const string LARGE_TEXT_KEY = "Discord.Default.LargeText.Nightly";
        private const string LARGE_ICON_KEY = "icon_nightly";
#else
        private const string LARGE_TEXT_KEY = "Discord.Default.LargeText.Stable";
        private const string LARGE_ICON_KEY = "icon_stable";
#endif

        private Discord.Discord _discord;

        private bool _wasInGameplay;
        private bool _wasPaused;

        private long _gameStartTime;
        private long _songStartTime;
        private long _pauseTime;

        private void Start()
        {
            // Listen to the changing of states
            GameStateFetcher.GameStateChange += OnGameStateChange;

            // Create the Discord instance
            try
            {
                _discord = new Discord.Discord(APPLICATION_ID, (ulong) CreateFlags.NoRequireDiscord);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to start Discord presence client.");
                Debug.LogException(e);

                _discord = null;
                return;
            }

            // Get the start time of the game (Discord requires it in this format)
            _gameStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Set default activity
            SetDefaultActivity();
        }

        private void OnGameStateChange(GameStateFetcher.State state)
        {
            if (state.CurrentScene != SceneIndex.Gameplay)
            {
                _wasInGameplay = false;
                _wasPaused = false;

                SetDefaultActivity();
                return;
            }

            var song = state.SongMetadata;

            if (song is null)
            {
                return;
            }

            // If it's the first time entering the song, collect all the info we need

            if (!_wasInGameplay)
            {
                _songStartTime = GetUnixTime();
            }

            _wasInGameplay = true;

            // Deal with pausing

            if (state.Paused)
            {
                // Deal with starting to pause...

                _pauseTime = GetUnixTime();

                _wasPaused = true;
            }
            else if (_wasPaused)
            {
                // Deal with unpausing...

                var timePausedFor = GetUnixTime() - _pauseTime;
                _songStartTime += timePausedFor;

                _wasPaused = false;
            }

            // Localize Discord Rich Presence

            var discordDetails = LocaleHelper.StringReference("Discord.Song.Name");
            discordDetails.Arguments = new object[]
            {
                song.Name, song.Artist
            };

            LocalizedString discordState;
            if (state.Paused)
            {
                discordState = LocaleHelper.StringReference("Discord.Song.Paused");
            }
            else
            {
                discordState = LocaleHelper.StringReference("Discord.Song.Album");
                discordState.Arguments = new object[]
                {
                    song.Album
                };
            }

            // Get activity

            var activity = new Activity
            {
                Assets =
                {
                    // The image and key are defined in the Discord developer portal
                    LargeImage = LARGE_ICON_KEY,
                    LargeText = LocaleHelper.LocalizeString(LARGE_TEXT_KEY)
                },
                Details = discordDetails.GetLocalizedString(),
                State = discordState.GetLocalizedString(),
                Timestamps =
                {
                    // If it's paused, don't show the time elapsed
                    Start = state.Paused ? 0 : _songStartTime
                }
            };

            // Set activity

            SetActivity(activity);
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

        private void OnApplicationQuit()
        {
            TryDispose();
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

        private void SetActivity(Activity activity)
        {
            _discord?.GetActivityManager().UpdateActivity(activity, result =>
            {
                if (result != Result.Ok)
                {
                    Debug.LogWarning("Discord Activity Error: " + result);
                }
            });
        }

        private void SetDefaultActivity()
        {
            SetActivity(new Activity
            {
                Assets =
                {
                    // The image and key are defined in the Discord developer portal
                    LargeImage = LARGE_ICON_KEY,
                    LargeText = LocaleHelper.LocalizeString(LARGE_TEXT_KEY)
                },
                Details = LocaleHelper.LocalizeString("Discord.Default.Details"),
                State = LocaleHelper.LocalizeString("Discord.Default.State"),
                Timestamps =
                {
                    Start = _gameStartTime
                }
            });
        }

        private static long GetUnixTime()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
}