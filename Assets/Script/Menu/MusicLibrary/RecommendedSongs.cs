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
        public const int RECOMMEND_SONGS_COUNT = 10;

        public static SongEntry[] GetRecommendedSongs(System.Func<SongEntry, bool> predicate)
        {
            var eligibleSongs = predicate == null
                ? SongContainer.Songs.ToList()
                : SongContainer.Songs.Where(predicate).ToList();
            if (eligibleSongs.Count == 0)
            {
                return System.Array.Empty<SongEntry>();
            }

            var eligibleSet = eligibleSongs.ToHashSet();
            var songs = new SongEntry[RECOMMEND_SONGS_COUNT];
            int index = 0;
            AddMostPlayedSongs(songs, ref index, eligibleSet);
            AddRandomSongs(songs, ref index, eligibleSongs);
            return songs[..index];
        }

        private static void AddMostPlayedSongs(SongEntry[] songs, ref int index, HashSet<SongEntry> eligibleSongs)
        {
            const float RNG_PER_SONG = .05f;

            var mostPlayed = ScoreContainer.GetMostPlayedSongs(10, eligibleSongs.Contains);
            if (mostPlayed.Count > 0)
            {
                float rng = mostPlayed.Count * RNG_PER_SONG;
                if (Random.value < rng)
                {
                    AddSongFromMostPlayed(songs, ref index, mostPlayed);
                }
                AddSongsFromTopPlayedArtists(songs, ref index, mostPlayed, eligibleSongs);
            }
        }

        private static readonly SortString _YARGSOURCE = new SortString("yarg");
        private static void AddRandomSongs(SongEntry[] songs, ref int index, List<SongEntry> eligibleSongs)
        {
            const float STARTING_RNG = .75f;
            const float RNG_DECREMENT = .25f;

            SongContainer.Sources.TryGetValue(_YARGSOURCE, out var yargSongs);
            var eligibleYargSongs = yargSongs?.Where(eligibleSongs.Contains).ToList();

            foreach (var song in songs[..index])
            {
                eligibleSongs.Remove(song);
                eligibleYargSongs?.Remove(song);
            }

            float yargSongRNG = eligibleYargSongs is { Count: > 0 } ? STARTING_RNG : 0;
            while (index < RECOMMEND_SONGS_COUNT && eligibleSongs.Count > 0)
            {
                SongEntry song;
                if (eligibleYargSongs is { Count: > 0 } && Random.value <= yargSongRNG)
                {
                    yargSongRNG -= RNG_DECREMENT;
                    song = eligibleYargSongs.Pick();
                }
                else
                {
                    song = eligibleSongs.Pick();
                }

                songs[index++] = song;
                eligibleSongs.Remove(song);
                eligibleYargSongs?.Remove(song);
            }
        }

        private static void AddSongFromMostPlayed(SongEntry[] songs, ref int index, List<SongEntry> mostPlayed)
        {
            int songIndex = Random.Range(0, mostPlayed.Count);
            var song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            songs[index++] = song;
        }

        private static void AddSongsFromTopPlayedArtists(SongEntry[] songs, ref int index,
            List<SongEntry> mostPlayed, HashSet<SongEntry> eligibleSongs)
        {
            var artists = SongContainer.Artists;
            while (mostPlayed.Count > 0)
            {
                int songIndex = Random.Range(0, mostPlayed.Count);
                var artistSongs = artists[mostPlayed[songIndex].Artist]
                    .Where(eligibleSongs.Contains)
                    .Where(song => !mostPlayed.Contains(song) && !songs.Contains(song))
                    .ToList();
                if (artistSongs.Count > 0)
                {
                    songs[index++] = artistSongs.Pick();
                    break;
                }
                mostPlayed.RemoveAt(songIndex);
            }
        }
    }
}
