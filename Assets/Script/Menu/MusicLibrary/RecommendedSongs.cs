using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public static class RecommendedSongs
    {
        public const int RECOMMEND_SONGS_COUNT = 10;

        public static SongEntry[] GetRecommendedSongs()
        {
            var songs = new SongEntry[RECOMMEND_SONGS_COUNT];
            int index = 0;
            var isPlayable = BuildPlayablePredicate();
            AddMostPlayedSongs(songs, ref index, isPlayable);
            AddRandomSongs(songs, ref index, isPlayable);
            return songs[..index];
        }

        private static void AddMostPlayedSongs(SongEntry[] songs, ref int index, System.Func<SongEntry, bool> isPlayable)
        {
            const float RNG_PER_SONG = .05f;

            var mostPlayed = ScoreContainer.GetMostPlayedSongs(10).Where(isPlayable).ToList();
            if (mostPlayed.Count > 0)
            {
                float rng = mostPlayed.Count * RNG_PER_SONG;
                if (Random.value < rng)
                {
                    AddSongFromMostPlayed(songs, ref index, ref mostPlayed);
                }
                AddSongsFromTopPlayedArtists(songs, ref index, ref mostPlayed, isPlayable);
            }
        }

        private static readonly SortString _YARGSOURCE = new SortString("yarg");
        private static void AddRandomSongs(SongEntry[] songs, ref int index, System.Func<SongEntry, bool> isPlayable)
        {
            const float STARTING_RNG = .75f;
            const float RNG_DECREMENT = .25f;
            const int MAX_TRIES = 50;

            SongContainer.Sources.TryGetValue(_YARGSOURCE, out var yargSongs);

            float yargSongRNG = yargSongs != null ? STARTING_RNG : 0;
            int tries = 0;
            while (index < RECOMMEND_SONGS_COUNT && tries < MAX_TRIES)
            {
                SongEntry song;
                if (Random.value <= yargSongRNG)
                {
                    yargSongRNG -= RNG_DECREMENT;
                    song = yargSongs.Pick();
                }
                else
                {
                    song = SongContainer.GetRandomSong();
                }

                if (!songs.Contains(song) && isPlayable(song))
                {
                    songs[index++] = song;
                }
                tries++;
            }
        }

        private static void AddSongFromMostPlayed(SongEntry[] songs, ref int index, ref List<SongEntry> mostPlayed)
        {
            int songIndex = Random.Range(0, mostPlayed.Count);
            var song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            songs[index++] = song;
        }

        private static void AddSongsFromTopPlayedArtists(SongEntry[] songs, ref int index,
            ref List<SongEntry> mostPlayed, System.Func<SongEntry, bool> isPlayable)
        {
            var artists = SongContainer.Artists;
            while (mostPlayed.Count > 0)
            {
                int songIndex = Random.Range(0, mostPlayed.Count);
                var song = artists[mostPlayed[songIndex].Artist].Pick();
                if (!mostPlayed.Contains(song) && !songs.Contains(song) && isPlayable(song))
                {
                    songs[index++] = song;
                    break;
                }
                mostPlayed.RemoveAt(songIndex);
            }
        }
        
        private static System.Func<SongEntry, bool> BuildPlayablePredicate()
        {
            var playerInstruments = PlayerContainer.Players
                .Select(p => p.Profile.GameMode.PossibleInstruments().ToList())
                .ToList();

            if (playerInstruments.Count == 0)
                return _ => true;

            return song =>
            {
                bool fdOnly = SettingsManager.Settings.RequireAllDifficulties.Value;
                foreach (var instruments in playerInstruments)
                {
                    bool canPlay = false;
                    foreach (var instrument in instruments)
                    {
                        if (song.HasInstrument(instrument) &&
                            (!fdOnly || song.HasEmhxDifficultiesForInstrument(instrument)))
                        {
                            canPlay = true;
                            break;
                        }
                    }
                    if (!canPlay)
                    {
                        return false;
                    }

                }
                return true;
            };
        }
    }
}
