﻿using System.Collections.Generic;
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

            // If no songs were played, skip
            if (mostPlayed.Count <= 0)
            {
                return;
            }

            AddMostPlayedSongs(mostPlayed);
            AddSongsFromTopPlayedArtists(mostPlayed);
        }

        private static void AddMostPlayedSongs(List<SongMetadata> mostPlayed)
        {
            // Add two random top ten most played songs (ten tries each)
            for (int i = 0; i < 2; i++)
            {
                for (int t = 0; t < TRIES; t++)
                {
                    var song = mostPlayed.Pick();

                    if (_recommendedSongs.Contains(song))
                    {
                        continue;
                    }

                    _recommendedSongs.Add(song);
                    break;
                }
            }
        }

        private static void AddSongsFromTopPlayedArtists(List<SongMetadata> mostPlayed)
        {
            // Pick 1 or 2...
            int choices = 2;
            if (Random.value <= 0.5f)
            {
                choices = 1;
            }

            // ...random songs from artists that are in the most played (ten tries each)
            for (int i = 0; i < choices; i++)
            {
                for (int t = 0; t < TRIES; t++)
                {
                    // Pick a random song, and get all of the songs from that artist
                    var sameArtistSongs = GetAllSongsFromArtist(mostPlayed.Pick().Artist);

                    // If the artist only has one song, it is guaranteed to not pass the rest of the ifs
                    if (sameArtistSongs.Count <= 1)
                    {
                        continue;
                    }

                    // Pick a random song from that artist
                    var song = sameArtistSongs.Pick();

                    // Skip if included in most played songs
                    if (mostPlayed.Contains(song))
                    {
                        continue;
                    }

                    // Skip if already included in recommendedSongs
                    if (_recommendedSongs.Contains(song))
                    {
                        continue;
                    }

                    // Add
                    _recommendedSongs.Add(song);
                    break;
                }
            }
        }

        private static List<SongMetadata> GetAllSongsFromArtist(string artist)
        {
            return GlobalVariables.Instance.SongContainer.Songs
                .Where(i => RemoveDiacriticsAndArticle(i.Artist) == RemoveDiacriticsAndArticle(artist))
                .ToList();
        }

        private static string RemoveDiacriticsAndArticle(string value)
        {
            return SongSearching.RemoveDiacriticsAndArticle(value);
        }

        private static void AddRandomSong()
        {
            // Try to add a YARG setlist song (we love bias!)
            if (Random.value <= 0.6f)
            {
                var yargSongs = GlobalVariables.Instance.SongContainer.Songs
                    .Where(i => i.Source.SortStr == "yarg").ToList();

                // Skip if the user has no YARG songs :(
                if (yargSongs.Count <= 0)
                {
                    goto RandomSong;
                }

                var song = yargSongs.Pick();

                // Skip if the song was already in recommended
                if (_recommendedSongs.Contains(song))
                {
                    goto RandomSong;
                }

                // Add the YARG song
                _recommendedSongs.Add(song);

                return;
            }

        RandomSong:

            // Add a completely random song (ten tries)
            for (int t = 0; t < TRIES; t++)
            {
                var song = ((IList<SongMetadata>) GlobalVariables.Instance.SongContainer.Songs).Pick();

                if (_recommendedSongs.Contains(song))
                {
                    continue;
                }

                _recommendedSongs.Add(song);
                break;
            }
        }
    }
}