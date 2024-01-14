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
        private const int TRIES = 10;

        private static readonly List<SongMetadata> _recommendedSongs = new();

        public static List<SongMetadata> GetRecommendedSongs()
        {
            _recommendedSongs.Clear();

            AddMostPlayedSongs();

            // Fill the rest of the spaces with random songs
            int left = 5 - _recommendedSongs.Count;
            for (int i = 0; i < left; i++)
            {
                AddRandomSong();
            }

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

        private static void AddMostPlayedSongs()
        {
            // Get the top ten most played songs
            var mostPlayed = ScoreContainer.GetMostPlayedSongs(10);
            if (mostPlayed.Count > 0)
            {
                AddMostPlayedSongs(mostPlayed);
                AddSongsFromTopPlayedArtists(mostPlayed);
            }
        }

        private static void AddMostPlayedSongs(List<SongMetadata> mostPlayed)
        {
            // Add two random top ten most played songs (ten tries each)
            for (int i = 0; i < 2; i++)
            {
                for (int t = 0; t < TRIES; t++)
                {
                    var song = mostPlayed.Pick();
                    if (!_recommendedSongs.Contains(song))
                    {
                        _recommendedSongs.Add(song);
                        break;
                    }
                }
            }
        }

        private static void AddSongsFromTopPlayedArtists(List<SongMetadata> mostPlayed)
        {
            var artists = GlobalVariables.Instance.SongContainer.Artists;
            // Pick 1 or 2 random songs from artists that are in the most played (ten tries each)
            int choices = Random.Range(1, 3);
            for (int i = 0; i < choices; i++)
            {
                for (int t = 0; t < TRIES; t++)
                {
                    // Pick a random song made by an artist in the mostPlayed list
                    if (!artists.TryGetValue(mostPlayed.Pick().Artist, out var artistSongs))
                        continue;
                    var song = artistSongs.Pick();

                    // Add if not in most played songs and wasn't already recommended
                    if (!mostPlayed.Contains(song) && !_recommendedSongs.Contains(song))
                    {
                        _recommendedSongs.Add(song);
                        break;
                    }
                }
            }
        }

        private static void AddRandomSong()
        {
            // Try to add a YARG setlist song (we love bias!)
            if (Random.value <= 0.6f)
            {
                var sources = GlobalVariables.Instance.SongContainer.GetSortedSongList(SongAttribute.Source);
                if (sources.TryGetValue("yarg", out var yargSongs))
                {
                    var song = yargSongs.Pick();
                    if (!_recommendedSongs.Contains(song))
                    {
                        _recommendedSongs.Add(song);
                        return;
                    }
                }
            }

            // Add a completely random song (ten tries)
            for (int t = 0; t < TRIES; t++)
            {
                var song = GlobalVariables.Instance.SongContainer.GetRandomSong();
                if (!_recommendedSongs.Contains(song))
                {
                    _recommendedSongs.Add(song);
                    break;
                }
            }
        }
    }
}