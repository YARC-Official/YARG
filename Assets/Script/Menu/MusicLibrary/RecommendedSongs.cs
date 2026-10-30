using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Playlists;
using YARG.Scores;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public static class RecommendedSongs
    {
        private static readonly HashSet<HashWrapper> _recommendedThisSession = new();

        public static SongEntry[] GetRecommendedSongs(System.Func<SongEntry, bool> predicate,
            int recommendationCount)
        {
            List<SongEntry> eligibleSongs = new(SongContainer.Count);
            foreach (SongEntry song in SongContainer.Songs)
            {
                if (predicate == null || predicate(song))
                {
                    eligibleSongs.Add(song);
                }
            }
            // Recommendations provide no value when the complete filtered result set is already
            // small enough to display, and would only duplicate those songs above the main list.
            if (recommendationCount <= 0 || eligibleSongs.Count <= recommendationCount)
                return System.Array.Empty<SongEntry>();

            List<SongEntry> newEligibleSongs = new(eligibleSongs.Count);
            foreach (SongEntry song in eligibleSongs)
            {
                if (!_recommendedThisSession.Contains(song.Hash))
                {
                    newEligibleSongs.Add(song);
                }
            }

            HashSet<SongEntry> newEligibleSet = new(newEligibleSongs);
            SongEntry[] songs = new SongEntry[recommendationCount];
            int index = 0;
            AddMostPlayedSongs(songs, ref index, newEligibleSet);
            AddFavoriteSongs(songs, ref index, newEligibleSet);
            AddRandomSongs(songs, ref index, newEligibleSongs);

            // If the unseen pool is exhausted, allow songs recommended earlier this session
            // rather than returning fewer recommendations than the filtered library permits.
            if (index < recommendationCount)
                AddRandomSongs(songs, ref index, eligibleSongs);

            SongEntry[] recommendations = songs[..index];
            recommendations.Shuffle();
            foreach (SongEntry song in recommendations)
            {
                _recommendedThisSession.Add(song.Hash);
            }
            return recommendations;
        }

        private static void AddMostPlayedSongs(SongEntry[] songs, ref int index, HashSet<SongEntry> eligibleSongs)
        {
            int categoryLimit = Mathf.Max(1, songs.Length / 10);
            int mostPlayedCount = Random.Range(0, categoryLimit + 1);

            List<SongEntry> mostPlayed = ScoreContainer.GetMostPlayedSongs(
                Mathf.Max(10, categoryLimit), eligibleSongs.Contains);
            if (mostPlayed.Count > 0)
            {
                List<SongEntry> directCandidates = new(mostPlayed);
                while (mostPlayedCount-- > 0 && directCandidates.Count > 0 && index < songs.Length)
                    AddSongFromMostPlayed(songs, ref index, directCandidates);

                if (index < songs.Length)
                    AddSongsFromTopPlayedArtists(songs, ref index, mostPlayed, eligibleSongs, categoryLimit);
            }
        }

        private static readonly SortString _YARGSOURCE = new SortString("yarg");

        private static void AddFavoriteSongs(SongEntry[] songs, ref int index, HashSet<SongEntry> eligibleSongs)
        {
            int maxCount = Mathf.Max(2, songs.Length / 5);
            int count = Mathf.Min(Random.Range(0, maxCount + 1), songs.Length - index);
            HashSet<HashWrapper> favoriteHashes = new(PlaylistContainer.FavoritesPlaylist.SongHashes);
            List<SongEntry> favorites = new();
            foreach (SongEntry song in eligibleSongs)
            {
                if (favoriteHashes.Contains(song.Hash) && !ContainsSong(songs, index, song))
                {
                    favorites.Add(song);
                }
            }

            favorites.Shuffle();

            foreach (SongEntry song in favorites)
            {
                if (count-- <= 0) break;

                songs[index++] = song;
            }
        }

        private static void AddRandomSongs(SongEntry[] songs, ref int index, List<SongEntry> eligibleSongs)
        {
            const float STARTING_RNG = .75f;
            const float RNG_DECREMENT = .25f;

            List<SongEntry> eligibleYargSongs = null;
            if (SongContainer.Sources.TryGetValue(_YARGSOURCE, out List<SongEntry> yargSongs))
            {
                eligibleYargSongs = new List<SongEntry>();
                foreach (SongEntry song in yargSongs)
                {
                    if (eligibleSongs.Contains(song))
                    {
                        eligibleYargSongs.Add(song);
                    }
                }
            }

            for (int i = 0; i < index; i++)
            {
                SongEntry song = songs[i];
                eligibleSongs.Remove(song);
                eligibleYargSongs?.Remove(song);
            }

            float yargSongRNG = eligibleYargSongs is { Count: > 0 } ? STARTING_RNG : 0;
            while (index < songs.Length && eligibleSongs.Count > 0)
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
            SongEntry song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            songs[index++] = song;
        }

        private static void AddSongsFromTopPlayedArtists(SongEntry[] songs, ref int index,
            List<SongEntry> mostPlayed, HashSet<SongEntry> eligibleSongs, int count)
        {
            while (count > 0 && mostPlayed.Count > 0 && index < songs.Length)
            {
                int songIndex = Random.Range(0, mostPlayed.Count);
                List<SongEntry> songsByArtist = SongContainer.Artists[mostPlayed[songIndex].Artist];
                List<SongEntry> artistSongs = new();
                foreach (SongEntry song in songsByArtist)
                {
                    if (eligibleSongs.Contains(song) &&
                        !mostPlayed.Contains(song) &&
                        !ContainsSong(songs, index, song))
                    {
                        artistSongs.Add(song);
                    }
                }

                if (artistSongs.Count > 0)
                {
                    songs[index++] = artistSongs.Pick();
                    count--;
                }

                mostPlayed.RemoveAt(songIndex);
            }
        }

        private static bool ContainsSong(SongEntry[] songs, int count, SongEntry target)
        {
            for (int i = 0; i < count; i++)
                if (songs[i] == target) return true;

            return false;
        }
    }
}
