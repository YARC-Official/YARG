using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Cysharp.Threading.Tasks;
using Discord;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Localization;

namespace YARG.Integration
{
    public class DiscordController : MonoSingleton<DiscordController>
    {
        private const long APPLICATION_ID = 1091177744416637028;

        private const string ALBUM_API_URL = "https://api.enchor.us/album-art";

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

        // Keep track of this in case we want to update the value asynchronously
        private Activity _activity;

        private bool _wasInGameplay;
        private bool _wasPaused;

        private long _gameStartTime;
        private long _songStartTime;
        private long _pauseTime;

        private string _albumUrl;

        public void Initialize()
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
                YargLogger.LogException(e, "Failed to start Discord presence client.");

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
            // Skip if Discord is not initialized
            if (_discord is null)
            {
                return;
            }

            if (state.CurrentScene != SceneIndex.Gameplay)
            {
                _wasInGameplay = false;
                _wasPaused = false;

                _albumUrl = null;

                SetDefaultActivity();
                return;
            }

            var song = state.SongEntry;

            if (song is null)
            {
                return;
            }

            // If it's the first time entering the song, collect all the info we need
            if (!_wasInGameplay)
            {
                _songStartTime = GetUnixTime();
            }

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

            var discordDetails = Localize.KeyFormat("Discord.Song.Name", song.Name, song.Artist);

            string discordState;
            if (state.Paused)
            {
                discordState = Localize.Key("Discord.Song.Paused");
            }
            else
            {
                discordState = Localize.KeyFormat("Discord.Song.Album", song.Album);
            }

            // Get activity
            _activity = new Activity
            {
                Assets =
                {
                    LargeImage = LARGE_ICON_KEY,
                    LargeText = Localize.Key(LARGE_TEXT_KEY)
                },
                Details = discordDetails,
                State = discordState,
                Timestamps =
                {
                    // If it's paused, don't show the time elapsed
                    Start = state.Paused ? 0 : _songStartTime
                }
            };

            // If the album art was already loaded at this point, set it
            UpdateActivityWithAlbumArt();

            SetActivity(_activity);

            // Attempt to load album art (this will automatically update the activity)
            if (!_wasInGameplay && _albumUrl is null)
            {
                LoadAlbumArt(song).Forget();
            }

            _wasInGameplay = true;
        }

        private async UniTask LoadAlbumArt(SongEntry song)
        {
            // Try to get the album art from the Chorus Encore API
            try
            {
                using var client = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(ALBUM_API_URL),
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["name"] = song.Name,
                        ["artist"] = song.Artist,
                        ["charter"] = song.Charter
                    })
                };

                var response = await client.SendAsync(request);

                // If it's a 404 (the chart was not found) we can safely skip
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                // If it's a non-404 and a non-success code, we should throw
                response.EnsureSuccessStatusCode();

                _albumUrl = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to fetch album art for song.");
            }

            UpdateActivityWithAlbumArt();
            SetActivity(_activity);
        }

        private void Update()
        {
            if (_discord is null)
            {
                return;
            }

            try
            {
                _discord.RunCallbacks();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to run Discord client callbacks!");

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
                YargLogger.LogException(e, "Failed to dispose of Discord client.");
            }
        }

        private void UpdateActivityWithAlbumArt()
        {
            if (_albumUrl is null)
            {
                return;
            }

            // Make the small icon YARG instead of the big one
            _activity.Assets.SmallImage = LARGE_ICON_KEY;

            // Discord supports URLs as the large image
            _activity.Assets.LargeImage = _albumUrl;
        }

        private void SetActivity(Activity activity)
        {
            _discord?.GetActivityManager().UpdateActivity(activity, result =>
            {
                if (result != Result.Ok)
                {
                    YargLogger.LogFormatWarning("Discord Activity Error: ", result);
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
                    LargeText = Localize.Key(LARGE_TEXT_KEY)
                },
                Details = Localize.Key("Discord.Default.Details"),
                State = Localize.Key("Discord.Default.State"),
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