using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Player;
using YARG.Playlists;
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
        private bool IsSongPlayable(SongEntry song)
        {
            List<Instrument> candidateInstruments = new();
            foreach (var player in _players)
            {
                bool playable = false;
                foreach(var instrument in _instruments[player])
                {
                    if (song.HasInstrument(instrument))
                    {
                        playable = true;
                        break;
                    }
                }

                if (!playable)
                {
                    return false;
                }

                // We also need to check if this song shares at least one instrument with the other songs already in ShowPlaylist
                var playlist = _library.ShowPlaylist;

                // No need to check if this is the first in the list
                if (playlist.Count == 0)
                {
                    continue;
                }

                candidateInstruments.Clear();
                foreach (var instrument in _instruments[player])
                {
                    if (song.HasInstrument(instrument))
                    {
                        candidateInstruments.Add(instrument);
                    }
                }

                foreach (var songHash in playlist.SongHashes)
                {
                    // We know this hash exists, because we are only working on the show playlist,
                    // and we added it to the list ourselves
                    bool hasCommonInstrument = false;
                    foreach (var instrument in candidateInstruments)
                    {
                        if (SongContainer.SongsByHash[songHash][0].HasInstrument(instrument))
                        {
                            hasCommonInstrument = true;
                            break;
                        }
                    }

                    if (!hasCommonInstrument)
                    {
                        return false;
                    }
                }
            }

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
            out SortString subcategory, string[] invalidKeys = null)
        {
            invalidKeys ??= Array.Empty<string>();

            List<SortString> validKeys = new();
            // We need to pick a key that has at least MIN_SONGS_PER_CATEGORY songs in it
            foreach (var key in container.Keys)
            {
                if (!invalidKeys.Contains(key) && container[key].Count >= MIN_SONGS_PER_CATEGORY)
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

        private static ShowCategory RandomSource()
        {
            // Pick a random source from the available sources
            if (!TryChooseSubcategory(SongContainer.Sources, out var source))
            {
                return RandomSong();
            }

            var song = SongContainer.Sources[source].ElementAt(Rng.Next(0, SongContainer.Sources[source].Count));
            var sourceDisplay = SongSources.SourceToGameName(source);

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromSource", sourceDisplay), song);
        }

        private static ShowCategory RandomArtist()
        {
            // Pick a random artist from the available artists
            if (!TryChooseSubcategory(SongContainer.Artists, out var artist))
            {
                return RandomSong();
            }

            var song = SongContainer.Artists[artist].ElementAt(Rng.Next(0, SongContainer.Artists[artist].Count));

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromArtist", artist), song);
        }

        private static ShowCategory RandomGenre()
        {
            // Pick a random genre from the available genres
            if (!TryChooseSubcategory(SongContainer.Genres, out var genre))
            {
                return RandomSong();
            }

            // Pick a random song from the genre
            var song = SongContainer.Genres[genre].ElementAt(Rng.Next(0, SongContainer.Genres[genre].Count));

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

            // Pick a random decade (that is actually a number) from the list
            string decade;
            int tries = 0;
            int categoryCount;
            do
            {
                decade = SongContainer.Years.Keys.ElementAt(Rng.Next(0, SongContainer.Years.Count));
                categoryCount = SongContainer.Years[decade].Count;
                tries++;
            } while ((decade == "####" || categoryCount < MIN_SONGS_PER_CATEGORY) && tries < MAX_TRIES);

            // We can accept lower than ideal category count if we've tried repeatedly, but I'm not returning "####" as a decade
            if (decade == "####")
            {
                return RandomSong();
            }

            var outsong = SongContainer.Years[decade].ElementAt(Rng.Next(0, SongContainer.Years[decade].Count));

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromDecade", decade), outsong);
        }

        private static ShowCategory ShortSong()
        {
            // Get all the songs less than 2 minutes long (because that's what SongCache already knows)
            var songs = SongContainer.SongLengths["00:00 - 02:00"];
            var outsong = songs.ElementAt(Rng.Next(0, songs.Count));
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.ShortSong"), outsong);
        }

        private static ShowCategory LongSong()
        {
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

            var outsong = songs.ElementAt(Rng.Next(0, songs.Count));
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.LongSong"), outsong);
        }

        private static ShowCategory SongStartsWith()
        {
            // Pick a random letter, check if we have any songs starting with it

            string key = SongContainer.Titles.Keys.ElementAt(Rng.Next(0, SongContainer.Titles.Count));

            var songs = SongContainer.Titles[key];
            var song = songs.ElementAt(Rng.Next(0, songs.Count));

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
            var hash = playlist.SongHashes.ElementAt(Rng.Next(0, playlist.SongHashes.Count));

            // Sometimes a playlist contains a song hash we no longer have
            if (!SongContainer.SongsByHash.ContainsKey(hash))
            {
                return RandomSong();
            }

            return new ShowCategory(Localize.KeyFormat("Menu.MusicLibrary.PlayAShow.SongFromPlaylist", playlist.Name),
                SongContainer.SongsByHash[hash][0]);
        }

        private static ShowCategory SongFromFavorites()
        {
            var playlist = PlaylistContainer.FavoritesPlaylist;

            if (playlist == null || playlist.SongHashes.Count < MIN_SONGS_PER_CATEGORY)
            {
                return RandomSong();
            }

            // Get random song from favorites playlist
            var hash = playlist.SongHashes.ElementAt(Rng.Next(0, playlist.SongHashes.Count));

            // Sometimes a playlist contains a song hash we no longer have
            if (!SongContainer.SongsByHash.ContainsKey(hash))
            {
                return RandomSong();
            }

            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.SongFromFavorites"), SongContainer.SongsByHash[hash][0]);
        }

        private static ShowCategory RandomSong()
        {
            // Get a random song
            var outsong = SongContainer.GetRandomSong();
            return new ShowCategory(Localize.Key("Menu.MusicLibrary.PlayAShow.RandomSong"), outsong);
        }
    }
}