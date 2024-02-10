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
        public static List<SongMetadata> GetRecommendedSongs()
        {
            _recommendedSongs.Clear();

            AddMostPlayedSongs();
            AddRandomSongs();

            // YARG songs first
            _recommendedSongs.Sort((x, y) =>
            {
                // This is technically YARG songs last because of the reverse below
                if (x.Source.SortStr == "yarg") return -1;
                if (y.Source.SortStr == "yarg") return 1;
                return 0;
            });

            // Reverse (because that's how they are added to the song select)
            _recommendedSongs.Reverse();

            return _recommendedSongs;
        }

        private static readonly List<SongMetadata> _recommendedSongs = new(5);
        private static void AddMostPlayedSongs()
        {
            const float RNG_PER_SONG = .05f;

            // Get the top ten most played songs
            var mostPlayed = ScoreContainer.GetMostPlayedSongs(10);
            if (mostPlayed.Count > 0)
            {
                float rng = mostPlayed.Count * RNG_PER_SONG;
                if (Random.value < rng)
                {
                    AddSongFromMostPlayed(ref mostPlayed);
                }
                AddSongsFromTopPlayedArtists(ref mostPlayed);
            }
        }

        private static readonly SortString _YARGSOURCE = "yarg";
        private static void AddRandomSongs()
        {
            const float STARTING_RNG = .75f;
            const float RNG_DECREMENT = .25f;

            var sources = GlobalVariables.Instance.SongContainer.Sources;
            sources.TryGetValue(_YARGSOURCE, out var yargSongs);

            float yargSongRNG = yargSongs != null ? STARTING_RNG : 0;
            while (_recommendedSongs.Count < 5)
            {
                SongMetadata song;
                if (Random.value <= yargSongRNG)
                {
                    yargSongRNG -= RNG_DECREMENT;
                    song = yargSongs.Pick();
                }
                else
                {
                    song = GlobalVariables.Instance.SongContainer.GetRandomSong();
                }

                if (!_recommendedSongs.Contains(song))
                {
                    _recommendedSongs.Add(song);
                }
            }
        }

        private static void AddSongFromMostPlayed(ref List<SongMetadata> mostPlayed)
        {
            int songIndex = Random.Range(0, mostPlayed.Count);
            var song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            _recommendedSongs.Add(song);
        }

        private static void AddSongsFromTopPlayedArtists(ref List<SongMetadata> mostPlayed)
        {
            var artists = GlobalVariables.Instance.SongContainer.Artists;
            while (mostPlayed.Count > 0)
            {
                int songIndex = Random.Range(0, mostPlayed.Count);
                var song = artists[mostPlayed[songIndex].Artist].Pick();
                if (!mostPlayed.Contains(song) && !_recommendedSongs.Contains(song))
                {
                    _recommendedSongs.Add(song);
                    break;
                }
                mostPlayed.RemoveAt(songIndex);
            }
        }
    }
}