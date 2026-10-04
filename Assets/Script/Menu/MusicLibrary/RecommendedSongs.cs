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
        public const int RECOMMEND_SONGS_COUNT = 10;

        private static readonly HashSet<SongEntry> _recommendedThisSession = new();

        public static SongEntry[] GetRecommendedSongs(System.Func<SongEntry, bool> predicate)
        {
            List<SongEntry> eligibleSongs = new(SongContainer.Count);
            foreach (SongEntry song in SongContainer.Songs)
            {
                if (predicate == null || predicate(song))
                {
                    eligibleSongs.Add(song);
                }
            }
            if (eligibleSongs.Count == 0)
                return System.Array.Empty<SongEntry>();

            List<SongEntry> newEligibleSongs = new(eligibleSongs.Count);
            foreach (SongEntry song in eligibleSongs)
            {
                if (!_recommendedThisSession.Contains(song))
                {
                    newEligibleSongs.Add(song);
                }
            }

            HashSet<SongEntry> newEligibleSet = new(newEligibleSongs);
            SongEntry[] songs = new SongEntry[RECOMMEND_SONGS_COUNT];
            int index = 0;
            AddMostPlayedSongs(songs, ref index, newEligibleSet);
            AddFavoriteSongs(songs, ref index, newEligibleSet);
            AddRandomSongs(songs, ref index, newEligibleSongs);

            // If the unseen pool is exhausted, allow songs recommended earlier this session
            // rather than returning fewer recommendations than the filtered library permits.
            if (index < RECOMMEND_SONGS_COUNT)
                AddRandomSongs(songs, ref index, eligibleSongs);

            SongEntry[] recommendations = songs[..index];
            recommendations.Shuffle();
            _recommendedThisSession.UnionWith(recommendations);
            return recommendations;
        }

        private static void AddMostPlayedSongs(SongEntry[] songs, ref int index, HashSet<SongEntry> eligibleSongs)
        {
            const float RNG_PER_SONG = .05f;

            List<SongEntry> mostPlayed = ScoreContainer.GetMostPlayedSongs(10, eligibleSongs.Contains);
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

        private static void AddFavoriteSongs(SongEntry[] songs, ref int index, HashSet<SongEntry> eligibleSongs)
        {
            int count = Mathf.Min(Random.Range(0, 3), RECOMMEND_SONGS_COUNT - index);
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
            SongEntry song = mostPlayed[songIndex];
            mostPlayed.RemoveAt(songIndex);
            songs[index++] = song;
        }

        private static void AddSongsFromTopPlayedArtists(SongEntry[] songs, ref int index,
            List<SongEntry> mostPlayed, HashSet<SongEntry> eligibleSongs)
        {
            while (mostPlayed.Count > 0)
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
                    break;
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
