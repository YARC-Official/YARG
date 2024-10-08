using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public static class RecommendedSongs
    {
        public const int RECOMMEND_SONGS_COUNT = 5;

        public static SongEntry[] GetRecommendedSongs()
        {
            var songs = new SongEntry[RECOMMEND_SONGS_COUNT];
            int index = 0;
            AddMostPlayedSongs(songs, ref index);
            AddRandomSongs(songs, ref index);
            return songs[..index];
        }

        private static void AddMostPlayedSongs(SongEntry[] songs, ref int index)
        {
            const float RNG_PER_SONG = .05f;

            // Get the top ten most played songs
            var mostPlayed = ScoreContainer.GetMostPlayedSongs(10);
            if (mostPlayed.Count > 0)
            {
                float rng = mostPlayed.Count * RNG_PER_SONG;
                if (Random.value < rng)
                {
                    AddSongFromMostPlayed(songs, ref index, ref mostPlayed);
                }
                AddSongsFromTopPlayedArtists(songs, ref index, ref mostPlayed);
            }
        }

        private static readonly SortString _YARGSOURCE = "yarg";
        private static void AddRandomSongs(SongEntry[] songs, ref int index)
        {
            const float STARTING_RNG = .75f;
            const float RNG_DECREMENT = .25f;

            SongContainer.Sources.TryGetValue(_YARGSOURCE, out var yargSongs);

            float yargSongRNG = yargSongs != null ? STARTING_RNG : 0;
            while (index < RECOMMEND_SONGS_COUNT)
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

                if (!songs.Contains(song))
                {
                    songs[index++] = song;
                }
            }
        }

        private static void AddSongFromMostPlayed(SongEntry[] songs, ref int index, ref List<SongEntry> mostPlayed)
        {
            int songIndex = Random.Range(0, mostPlayed.Count);
            var song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            songs[index++] = song;
        }

        private static void AddSongsFromTopPlayedArtists(SongEntry[] songs, ref int index, ref List<SongEntry> mostPlayed)
        {
            var artists = SongContainer.Artists;
            while (mostPlayed.Count > 0)
            {
                int songIndex = Random.Range(0, mostPlayed.Count);
                var song = artists[mostPlayed[songIndex].Artist].Pick();
                if (!mostPlayed.Contains(song) && !songs.Contains(song))
                {
                    songs[index++] = song;
                    break;
                }
                mostPlayed.RemoveAt(songIndex);
            }
        }
    }
}