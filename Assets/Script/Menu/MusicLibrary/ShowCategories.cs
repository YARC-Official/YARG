using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.Filters;
using YARG.Player;
using YARG.Playlists;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class ShowCategories
    {
        public struct ShowCategory
        {
            public string    CategoryText;
            public SongEntry Song;

            public ShowCategory(string categoryText, SongEntry song)
            {
                CategoryText = categoryText;
                Song = song;
            }
        }

        private struct ShowCategoryType : IEquatable<ShowCategoryType>
        {
            public int Chance;
            public Func<ShowCategory> CategoryAction;

            public bool Equals(ShowCategoryType other) => CategoryAction.Equals(other.CategoryAction);

            public override bool Equals(object obj) => obj is ShowCategoryType other && Equals(other);

            public override int GetHashCode() => CategoryAction.GetHashCode();
        }

        private static readonly ShowCategory[]         Categories = new ShowCategory[5];
        private static readonly List<ShowCategoryType> UsedCategoryTypes = new List<ShowCategoryType>();
        private static readonly Random                 Rng        = new();
        private                 List<ShowCategoryType> _possibleCategories;

        private readonly Dictionary<YargPlayer, List<Instrument>> _instruments = new();
        private          List<YargPlayer>                         _players     = new();
        private readonly MusicLibraryMenu                         _library;

        private static readonly int MAX_TRIES = 10;
        private static readonly int MIN_SONGS_PER_CATEGORY = 4;

        public ShowCategories(MusicLibraryMenu library)
        {
            _library = library;
            GetProfileInstruments();
            CreateCategoryTypes();
            BuildCategoryList();
        }

        public void Refresh()
        {
            BuildCategoryList();
        }

        public ShowCategory[] GetCategories()
        {
            return Categories;
        }

        private void CreateCategoryTypes()
        {
            _possibleCategories = new List<ShowCategoryType>
            {
                new ShowCategoryType {Chance = 13, CategoryAction = RandomSource},
                new ShowCategoryType {Chance = 12, CategoryAction = RandomArtist},
                new ShowCategoryType {Chance = 15, CategoryAction = RandomGenre},
                new ShowCategoryType {Chance = 10, CategoryAction = RandomDecade},
                new ShowCategoryType {Chance = 1, CategoryAction = RandomSong},
                new ShowCategoryType {Chance = 15, CategoryAction = ShortSong},
                new ShowCategoryType {Chance = 10, CategoryAction = LongSong},
                new ShowCategoryType {Chance = 10, CategoryAction = SongStartsWith},
                new ShowCategoryType {Chance = 10, CategoryAction = SongFromPlaylist},
                new ShowCategoryType {Chance = 4, CategoryAction = SongFromFavorites},
            };
        }

        private void BuildCategoryList()
        {
            UsedCategoryTypes.Clear();
            for (int i = 0; i < Categories.Length; i++)
            {
                if (!PickSingleCategory(i))
                {
                    // We failed to pick a unique category, so try again. There are more types than slots, so
                    // this will eventually complete.
                    i--;
                }
            }
        }

        private bool PickSingleCategory(int i)
        {
            // Pick a random category based on Chance
            var p = Rng.Next(0, 100);
            foreach (var category in _possibleCategories)
            {
                if (p < category.Chance)
                {
                    // Get a playable song from the category
                    int tries = 0;
                    do
                    {
                        Categories[i] = category.CategoryAction();
                        tries++;
                    } while (!IsSongPlayable(Categories[i].Song) && tries < 5);

                    if (tries == MAX_TRIES)
                    {
                        // We exhausted tries for this category, so move on to the next category
                        continue;
                    }

                    // Check if we already used this category
                    bool reused = false;

                    for (int j = 0; j < i; j++)
                    {
                        if (UsedCategoryTypes.Contains(category))
                        {
                            reused = true;
                            break;
                        }
                    }

                    if (reused)
                    {
                        continue;
                    }

                    // Success, so category is now used and we need not loop again

                    UsedCategoryTypes.Add(category);
                    break;
                }

                // We'll get there eventually since the chances add up to 100.
                p -= category.Chance;
            }

            // Just in case we failed to pick a category...
            if (Categories[i].CategoryText == null)
            {
                return false;
            }

            return true;
        }

        // Song is playable only if all players can play and there is playable instrument commonality with the
        // rest of the show playlist.
        // When RequireAllDifficulties is set, also checks that full difficulty is available for at least one
        // of each player's possible instruments
        private bool IsSongPlayable(SongEntry song)
        {
            bool fdOnly = SettingsManager.Settings.RequireAllDifficulties.Value;
            var playableInstruments = new List<Instrument>();
            foreach (var player in _players)
            {
                playableInstruments.Clear();

                // Get the set of instruments this player can play on the new song.
                foreach (var instrument in _instruments[player])
                {
                    if (song.HasInstrument(instrument) && (!fdOnly || song.HasEmhxDifficultiesForInstrument(instrument)))
                    {
                        playableInstruments.Add(instrument);
                    }
                }

                // If the player can't play this song at all, it's not playable.
                if (playableInstruments.Count == 0)
                {
                    return false;
                }

                // If this is the first song, no need to check for commonality.
                var playlist = _library.ShowPlaylist;
                if (playlist.Count == 0)
                {
                    continue;
                }

                // Now, filter this set of instruments by what's available in the other songs
                foreach (var songHash in playlist.SongHashes)
                {
                    var playlistSong = SongContainer.SongsByHash[songHash][0];

                    // Find instruments in common with this playlist song by removing ones that don't match
                    for (int i = playableInstruments.Count - 1; i >= 0; i--)
                    {
                        var instrument = playableInstruments[i];
                        if (!playlistSong.HasInstrument(instrument) ||
                            (fdOnly && !playlistSong.HasEmhxDifficultiesForInstrument(instrument)))
                        {
                            playableInstruments.RemoveAt(i);
                        }
                    }

                    // If there are no common instruments left this song is not playable for this player
                    if (playableInstruments.Count == 0)
                    {
                        return false;
                    }
                }
            }
            
            // We didn't return earlier, so the song must be playable
            return true;
        }

        private void GetProfileInstruments()
        {
            _instruments.Clear();
            _players = PlayerContainer.Players.Where(e => !e.Profile.IsBot).ToList();
            foreach (var player in _players)
            {
                _instruments[player] = new List<Instrument>();
                // Add the player's possible instruments to the list
                foreach (var instrument in player.Profile.GameMode.PossibleInstruments())
                {
                    _instruments[player].Add(instrument);
                }
            }
        }

        private static bool TryChooseSubcategory(IReadOnlyDictionary<SortString, List<SongEntry>> container,
            Func<SongEntry, bool> predicate, out SortString subcategory, string[] invalidKeys = null)
        {
            invalidKeys ??= Array.Empty<string>();

            List<SortString> validKeys = new();
            // We need to pick a key that has at least MIN_SONGS_PER_CATEGORY songs in it
            foreach (var key in container.Keys)
            {
                if (!invalidKeys.Contains(key.ToString()) &&
                    GetFilteredCount(container[key], predicate) >= MIN_SONGS_PER_CATEGORY)
                {
                    validKeys.Add(key);
                }
            }

            if (validKeys.Count == 0)
            {
                subcategory = SortString.Empty;
                return false;
            }

            // We now know we have at least one valid key, so pick one
            subcategory = validKeys[Rng.Next(0, validKeys.Count)];
            return true;
        }

        private static bool TryChooseSubcategory(IReadOnlyDictionary<string, List<SongEntry>> container,
            Func<SongEntry, bool> predicate, out string subcategory, string[] invalidKeys = null)
        {
            invalidKeys ??= Array.Empty<string>();

            List<string> validKeys = new();
            // We need to pick a key that has at least MIN_SONGS_PER_CATEGORY songs in it
            foreach (var key in container.Keys)
            {
                if (!invalidKeys.Contains(key) &&
                    GetFilteredCount(container[key], predicate) >= MIN_SONGS_PER_CATEGORY)
                {
                    validKeys.Add(key);
                }
            }

            if (validKeys.Count == 0)
            {
                subcategory = string.Empty;
                return false;
            }

            // We now know we have at least one valid key, so pick one
            subcategory = validKeys[Rng.Next(0, validKeys.Count)];
            return true;
        }

        private static Func<SongEntry, bool> GetActiveFilterPredicate()
        {
            var predicate = FiltersMenu.ActiveFilterPredicate;
            if (predicate == null)
            {
                return null;
            }

            // If no songs match, ignore the predicate to avoid empty selections.
            foreach (var song in SongContainer.Songs)
            {
                if (predicate(song))
                {
                    return predicate;
                }
            }

            return null;
        }

        private static int GetFilteredCount(IReadOnlyList<SongEntry> songs, Func<SongEntry, bool> predicate)
        {
            if (predicate == null)
            {
                return songs.Count;
            }

            int count = 0;
            foreach (var song in songs)
            {
                if (predicate(song))
                {
                    count++;
                }
            }

            return count;
        }

        private static List<SongEntry> GetFilteredSongs(IReadOnlyList<SongEntry> songs, Func<SongEntry, bool> predicate)
        {
            if (predicate == null)
            {
                return songs.ToList();
            }

            List<SongEntry> filtered = new();
            foreach (var song in songs)
            {
                if (predicate(song))
                {
                    filtered.Add(song);
                }
            }

            return filtered;
        }

        private static ShowCategory RandomSource()
        {
            var predicate = GetActiveFilterPredicate();

            // Pick a random source from the available sources
            if (!TryChooseSubcategory(SongContainer.Sources, predicate, out var source))
            {
                return RandomSong();
            }

            var songs = GetFilteredSongs(SongContainer.Sources[source], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];
            var sourceDisplay = SongSources.SourceToGameName(source);

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromSource", sourceDisplay), song);
        }

        private static ShowCategory RandomArtist()
        {
            var predicate = GetActiveFilterPredicate();

            // Pick a random artist from the available artists
            if (!TryChooseSubcategory(SongContainer.Artists, predicate, out var artist))
            {
                return RandomSong();
            }

            var songs = GetFilteredSongs(SongContainer.Artists[artist], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromArtist", artist), song);
        }

        private static ShowCategory RandomGenre()
        {
            var predicate = GetActiveFilterPredicate();

            // Pick a random genre from the available genres
            if (!TryChooseSubcategory(SongContainer.Genres, predicate, out var genre))
            {
                return RandomSong();
            }

            // Pick a random song from the genre
            var songs = GetFilteredSongs(SongContainer.Genres[genre], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];

            var genreString = $"{genre}";
            string outString;
            List<string> vowels = new List<string> {"a", "e", "i", "o", "u"};
            if (vowels.Contains(genreString.ToLower()[..1]))
            {
                outString = Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromAnGenre", genre);
            }
            else
            {
                outString = Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromGenre", genre);
            }

            return new ShowCategory(outString, song);
        }

        private static ShowCategory RandomDecade()
        {
            // Turns out SongContainer.Years should really be named SongContainer.Decades

            var predicate = GetActiveFilterPredicate();

            // Pick a random decade (that is actually a number) from the list
            string decade;
            int tries = 0;
            int categoryCount;
            do
            {
                decade = SongContainer.Years.Keys.ElementAt(Rng.Next(0, SongContainer.Years.Count));
                categoryCount = GetFilteredCount(SongContainer.Years[decade], predicate);
                tries++;
            } while ((decade == "####" || categoryCount < MIN_SONGS_PER_CATEGORY) && tries < MAX_TRIES);

            // We can accept lower than ideal category count if we've tried repeatedly, but I'm not returning "####" as a decade
            if (decade == "####")
            {
                return RandomSong();
            }

            var songs = GetFilteredSongs(SongContainer.Years[decade], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var outsong = songs[Rng.Next(0, songs.Count)];

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromDecade", decade), outsong);
        }

        private static ShowCategory ShortSong()
        {
            var predicate = GetActiveFilterPredicate();

            // Get all the songs less than 2 minutes long (because that's what SongCache already knows)
            var songs = GetFilteredSongs(SongContainer.SongLengths["00:00 - 02:00"], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var outsong = songs[Rng.Next(0, songs.Count)];
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.ShortSong"), outsong);
        }

        private static ShowCategory LongSong()
        {
            var predicate = GetActiveFilterPredicate();

            // Get all the songs greater than 5 minutes long
            List<SongEntry> songs = new();

            // Define all the time ranges we want to include
            string[] longSongKeys = {
                "05:00 - 10:00",
                "10:00 - 15:00",
                "15:00 - 20:00",
                "20:00+"
            };

            foreach (var range in longSongKeys)
            {
                if (SongContainer.SongLengths.ContainsKey(range))
                {
                    songs.AddRange(SongContainer.SongLengths[range]);
                }
            }

            var filtered = GetFilteredSongs(songs, predicate);
            if (filtered.Count == 0)
            {
                return RandomSong();
            }

            var outsong = filtered[Rng.Next(0, filtered.Count)];
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.LongSong"), outsong);
        }

        private static ShowCategory SongStartsWith()
        {
            var predicate = GetActiveFilterPredicate();

            // Pick a random letter, check if we have any songs starting with it

            if (!TryChooseSubcategory(SongContainer.Titles, predicate, out var key))
            {
                return RandomSong();
            }

            var songs = GetFilteredSongs(SongContainer.Titles[key], predicate);
            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];

            if (key == "0-9")
            {
                return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.StartsWithNumber"), song);
            }

            if (key == "*")
            {
                return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.StartsWithOther"), song);
            }

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.StartsWithLetter", key), song);
        }

        private static ShowCategory SongFromPlaylist()
        {
            var predicate = GetActiveFilterPredicate();
            var playlists = PlaylistContainer.Playlists.Where(e => e.Count > MIN_SONGS_PER_CATEGORY).ToList();

            for (int i = 0; i < playlists.Count; i++)
            {
                if (playlists[i].Count < 5 || playlists[i] == PlaylistContainer.FavoritesPlaylist)
                {
                    playlists.RemoveAt(i);
                    // Not sure if actually needed, but just in case..worst case we process some items we're keeping
                    // more than once
                    i--;
                }
            }

            if (playlists.Count == 0)
            {
                return RandomSong();
            }

            // Get random playlist from the ones that remain
            var playlist = playlists.ElementAt(Rng.Next(0, playlists.Count));

            // Get random song from said playlist
            List<SongEntry> songs = new();
            foreach (var hash in playlist.SongHashes)
            {
                // Sometimes a playlist contains a song hash we no longer have
                if (!SongContainer.SongsByHash.ContainsKey(hash))
                {
                    continue;
                }

                var entry = SongContainer.SongsByHash[hash][0];
                if (predicate == null || predicate(entry))
                {
                    songs.Add(entry);
                }
            }

            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];
            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromPlaylist", playlist.Name), song);
        }

        private static ShowCategory SongFromFavorites()
        {
            var predicate = GetActiveFilterPredicate();
            var playlist = PlaylistContainer.FavoritesPlaylist;

            if (playlist == null || playlist.SongHashes.Count < MIN_SONGS_PER_CATEGORY)
            {
                return RandomSong();
            }

            // Get random song from favorites playlist
            List<SongEntry> songs = new();
            foreach (var hash in playlist.SongHashes)
            {
                if (!SongContainer.SongsByHash.ContainsKey(hash))
                {
                    continue;
                }

                var entry = SongContainer.SongsByHash[hash][0];
                if (predicate == null || predicate(entry))
                {
                    songs.Add(entry);
                }
            }

            if (songs.Count == 0)
            {
                return RandomSong();
            }

            var song = songs[Rng.Next(0, songs.Count)];
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.SongFromFavorites"), song);
        }

        private static ShowCategory RandomSong()
        {
            var predicate = GetActiveFilterPredicate();

            // Get a random song
            if (predicate != null)
            {
                List<SongEntry> songs = new();
                foreach (var song in SongContainer.Songs)
                {
                    if (predicate(song))
                    {
                        songs.Add(song);
                    }
                }

                if (songs.Count > 0)
                {
                    var filteredSong = songs[Rng.Next(0, songs.Count)];
                    return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.RandomSong"), filteredSong);
                }
            }

            var outsong = SongContainer.GetRandomSong();
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.RandomSong"), outsong);
        }
    }
}
