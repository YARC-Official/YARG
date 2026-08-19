using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Playlists
{
    public static class PlaylistContainer
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new JsonHashWrapperConverter()
            }
        };

        public static string PlaylistDirectory { get; private set; }

        private static string _favoritesPath;

        private static List<Playlist> _playlists = new();

        public static IReadOnlyList<Playlist> Playlists => _playlists;

        public static Playlist FavoritesPlaylist { get; private set; }

        public static void Initialize()
        {
            PlaylistDirectory = Path.Combine(PathHelper.PersistentDataPath, "playlists");
            _favoritesPath = Path.Combine(PlaylistDirectory, "favorites.json");

            Directory.CreateDirectory(PlaylistDirectory);

            if (!File.Exists(_favoritesPath))
            {
                // If the favorites playlist doesn't exist, create one
                FavoritesPlaylist = new Playlist
                {
                    Name = "Favorites",
                    Author = "You",
                    Id = Guid.NewGuid(),
                    SongHashes = new List<HashWrapper>()
                };

                SavePlaylist(FavoritesPlaylist, _favoritesPath);
            }
            else
            {
                // If it does, load it in
                FavoritesPlaylist = LoadPlaylist(_favoritesPath);

                if (FavoritesPlaylist is null)
                {
                    FavoritesPlaylist = new Playlist
                    {
                        Name = "Favorites",
                        Author = "You",
                        Id = Guid.NewGuid(),
                        SongHashes = new List<HashWrapper>()
                    };

                    SavePlaylist(FavoritesPlaylist, _favoritesPath);
                }
            }

            // Load any other playlists found in the playlist folder
            foreach (var file in Directory.GetFiles(PlaylistDirectory))
            {
                if (file == _favoritesPath || !file.EndsWith(".json"))
                {
                    continue;
                }

                var playlist = LoadPlaylist(file);
                if (playlist is not null && playlist.Id != FavoritesPlaylist.Id)
                {
                    _playlists.Add(playlist);
                }
            }

            SortPlaylistsByName();
        }

        public static void SaveAll()
        {
            SavePlaylist(FavoritesPlaylist, _favoritesPath);
        }

        public static int RemoveDeadHashes(Playlist playlist, IReadOnlyDictionary<HashWrapper, List<SongEntry>> songsByHash)
        {
            if (playlist == null || songsByHash == null)
            {
                return 0;
            }

            int removed = 0;
            RemoveDeadHashesFromPlaylist(playlist, songsByHash, ref removed);

            if (removed > 0)
            {
                YargLogger.LogInfo($"Removed {removed} dead song hashes from playlist '{playlist.Name}'");
            }

            return removed;
        }

        public static int ReplaceUpdatedSongHashes(IReadOnlyDictionary<HashWrapper, HashWrapper> replacements)
        {
            if (replacements == null || replacements.Count == 0)
            {
                return 0;
            }

            int replaced = 0;

            void ReplaceAndSave(Playlist playlist, string path)
            {
                if (playlist?.SongHashes == null)
                {
                    return;
                }

                int playlistReplacements = 0;
                for (int i = 0; i < playlist.SongHashes.Count; i++)
                {
                    if (replacements.TryGetValue(playlist.SongHashes[i], out var replacement))
                    {
                        playlist.SongHashes[i] = replacement;
                        playlistReplacements++;
                    }
                }

                if (playlistReplacements == 0)
                {
                    return;
                }

                replaced += playlistReplacements;
                SavePlaylist(playlist, path);
                YargLogger.LogInfo($"Replaced {playlistReplacements} updated song hashes in playlist '{playlist.Name}'");
            }

            ReplaceAndSave(FavoritesPlaylist, _favoritesPath);
            foreach (var playlist in _playlists)
            {
                ReplaceAndSave(playlist, Path.Join(PlaylistDirectory, GetFileNameForPlaylist(playlist)));
            }

            return replaced;
        }

        private static bool RemoveDeadHashesFromPlaylist(Playlist playlist, IReadOnlyDictionary<HashWrapper, List<SongEntry>> songsByHash, ref int removed)
        {
            if (playlist.SongHashes == null || playlist.SongHashes.Count == 0)
            {
                return false;
            }

            int originalCount = playlist.SongHashes.Count;
            var kept = new List<HashWrapper>(originalCount);

            foreach (var hash in playlist.SongHashes)
            {
                if (songsByHash.ContainsKey(hash))
                {
                    kept.Add(hash);
                }
            }

            if (kept.Count == originalCount)
            {
                return false;
            }

            removed += originalCount - kept.Count;
            playlist.SongHashes.Clear();
            playlist.SongHashes.AddRange(kept);
            return true;
        }

        public static void SavePlaylist(Playlist playlist)
        {
            var path = Path.Join(PlaylistDirectory, GetFileNameForPlaylist(playlist));
            SavePlaylist(playlist, path);
        }

        private static void SavePlaylist(Playlist playlist, string path)
        {
            try
            {
                var text = JsonConvert.SerializeObject(playlist, _jsonSettings);
                File.WriteAllText(path, text);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to save playlist");
            }
        }

        private static void DeletePlaylistFile(Playlist playlist)
        {
            var path = Path.Join(PlaylistDirectory, GetFileNameForPlaylist(playlist));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static Playlist LoadPlaylist(string path)
        {
            try
            {
                var text = File.ReadAllText(path);
                var playlist = JsonConvert.DeserializeObject<Playlist>(text, _jsonSettings);

                return playlist;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to load playlist");
            }

            return null;
        }

        private static string GetFileNameForPlaylist(Playlist playlist)
        {
            // Limit the file name to 20 characters
            string fileName = playlist.Name;
            if (fileName.Length > 20)
            {
                fileName = fileName[..20];
            }

            // Remove symbols
            fileName = PathHelper.SanitizeFileName(fileName);

            // Add the end
            fileName += $".{playlist.Id.ToString()[..8]}.json";

            return fileName;
        }

        public static Playlist CreatePlaylist(string name)
        {
            var playlist = new Playlist
            {
                Name = name,
                Author = "You",
                Id = Guid.NewGuid(),
                SongHashes = new List<HashWrapper>()
            };

            SavePlaylist(playlist);
            _playlists.Add(playlist);
            SortPlaylistsByName();
            return playlist;
        }

        public static void DeletePlaylist(Playlist playlist)
        {
            _playlists.Remove(playlist);
            DeletePlaylistFile(playlist);
        }

        public static void RenamePlaylist(Playlist playlist, string newName)
        {
            // Delete old file
            DeletePlaylistFile(playlist);

            // Update name
            playlist.Name = newName;

            // Save with new name
            SavePlaylist(playlist);
            SortPlaylistsByName();
        }

        private static void SortPlaylistsByName()
        {
            _playlists.Sort((a, b) =>
            {
                string nameA = a?.Name ?? string.Empty;
                string nameB = b?.Name ?? string.Empty;
                string sortA = RichTextUtils.StripRichTextTags(nameA);
                string sortB = RichTextUtils.StripRichTextTags(nameB);

                int result = string.Compare(sortA, sortB, StringComparison.OrdinalIgnoreCase);
                return result != 0 ? result : string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
