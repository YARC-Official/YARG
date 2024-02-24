using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
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

        private static string _likedSongsPath;

        private static List<Playlist> _playlists = new();

        public static IReadOnlyList<Playlist> Playlists => _playlists;

        public static Playlist LikedSongsPlaylist { get; private set; }

        public static void Initialize()
        {
            PlaylistDirectory = Path.Combine(PathHelper.PersistentDataPath, "playlists");
            _likedSongsPath = Path.Combine(PlaylistDirectory, "likedSongs.json");

            Directory.CreateDirectory(PlaylistDirectory);

            if (!File.Exists(_likedSongsPath))
            {
                // If the liked songs playlist doesn't exist, create one
                LikedSongsPlaylist = new Playlist
                {
                    Name = "Liked Songs",
                    Author = "You",
                    Id = Guid.NewGuid(),
                    SongHashes = new List<HashWrapper>()
                };

                SavePlaylist(LikedSongsPlaylist, _likedSongsPath);
            }
            else
            {
                // If it does, load it in
                LikedSongsPlaylist = LoadPlaylist(_likedSongsPath);
            }
        }

        public static void SaveAll()
        {
            SavePlaylist(LikedSongsPlaylist, _likedSongsPath);
        }

        private static void SavePlaylist(Playlist playlist)
        {
            var path = Path.Join(PlaylistDirectory, GetFileNameForPlaylist(playlist));
            SavePlaylist(playlist, path);
        }

        private static void SavePlaylist(Playlist playlist, string path)
        {
            var text = JsonConvert.SerializeObject(playlist, _jsonSettings);
            File.WriteAllText(path, text);
        }

        private static Playlist LoadPlaylist(string path)
        {
            var text = File.ReadAllText(path);
            var playlist = JsonConvert.DeserializeObject<Playlist>(text, _jsonSettings);
            return playlist;
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
    }
}