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
        }

        public static void SaveAll()
        {
            SavePlaylist(FavoritesPlaylist, _favoritesPath);
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

        private static void DeletePlaylist(Playlist playlist)
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
            return playlist;
        }

        public static void RemovePlaylist(Playlist playlist)
        {
            _playlists.Remove(playlist);
            DeletePlaylist(playlist);
        }
    }
}