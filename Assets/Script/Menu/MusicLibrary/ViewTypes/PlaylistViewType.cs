using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Song;
using YARG.Playlists;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class PlaylistViewType : CategoryViewType
    {
        public override         BackgroundType          Background => BackgroundType.Normal;
        private static readonly Dictionary<int, Sprite> Sprites     = new();
        private const           string                  ICON_PATH   = "MusicLibraryIcons[Playlists]";
        private const           int                     PLAYLIST_ID = 1;
        public                  Playlist                Playlist;
        public                  int                     ID;
        private readonly        Sprite                  _sprite;

        public PlaylistViewType(string primary, int songCount, SongEntry[] songsUnderCategory, Playlist playlist,
            Action clickAction = null, int id = -1) : base(primary, songCount, songsUnderCategory, clickAction)
        {
            Playlist = playlist;
            ID = id;
        }

        // I do not like that GetSongsFromPlaylist has to do the work twice, but songs can't be cached since
        // it has to be static and I'm not sufficiently familiar with C# to figure out how to make CategoryViewType
        // use an initializer we can override.
        public PlaylistViewType(string primary, Playlist playlist, Action clickAction = null, int id = -1) :
            base(primary, GetSongsFromPlaylist(playlist).Length, GetSongsFromPlaylist(playlist), clickAction)
        {
            Playlist = playlist;
            ID = id;
        }

        public static SongEntry[] GetSongsFromPlaylist(Playlist playlist)
        {
            int songCount = playlist.SongHashes.Count;
            SongEntry[] songs = new SongEntry[songCount];
            int count = 0;
            foreach (var hash in playlist.SongHashes)
            {
                if (SongContainer.SongsByHash.TryGetValue(hash, out var song))
                {
                    songs[count++] = song[0];
                }
            }

            return songs[..count];
        }
    }
}