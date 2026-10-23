using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Menu.Persistent;
using YARG.Playlists;
using YARG.Settings;
using YARG.Song;

namespace YARG.Integration
{
    /// <summary>
    /// Pushes the live setlist to apps on this computer over a loopback socket while its setting is on.
    /// </summary>
    public class SetlistServer : MonoSingleton<SetlistServer>
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        public static bool IsSupported => true;
#else
        // UWP blocks loopback connections from other apps
        public static bool IsSupported => false;
#endif

        // Up/Down on the port setting moves one port per press, and rebinding on each would churn the listener
        private const float PORT_SETTLE_SECONDS = 1f;

        // A port another app holds, or a server that failed, is tried again this often, without another toast
        private const float RETRY_SECONDS = 10f;

        private SetlistSocketServer _server;
        private int   _wantedPort = -1;
        private float _wantedPortSince;
        private float _retryAt;
        private int   _toastedPort = -1;

        private readonly List<SongEntry> _songs = new();
        private bool _published;
        private int  _publishedSetlistRevision;
        private bool _publishedActive;
        private int  _publishedLibraryRevision;

        private void LateUpdate()
        {
            UpdateServer();
            if (_server != null)
            {
                PublishIfChanged();
            }
        }

        // Also runs before an editor script reload, which skips OnDestroy
        private void OnDisable()
        {
            StopServer();
        }

        private void UpdateServer()
        {
            var settings = SettingsManager.Settings;
            if (settings is null || !settings.SetlistServerEnable.Value)
            {
                StopServer();
                _retryAt = 0;
                _toastedPort = -1;
                return;
            }

            int port = settings.SetlistServerPort.Value;
            if (port != _wantedPort)
            {
                _wantedPort = port;
                _wantedPortSince = Time.unscaledTime;
                _retryAt = 0;
            }

            if (_server is { Faulted: true })
            {
                StopServer();
                _retryAt = Time.unscaledTime + RETRY_SECONDS;
            }

            if (_server?.Port == port || Time.unscaledTime - _wantedPortSince < PORT_SETTLE_SECONDS ||
                Time.unscaledTime < _retryAt)
            {
                return;
            }

            StopServer();
            try
            {
                _server = SetlistSocketServer.Start(port);
                _published = false;
                _toastedPort = -1;
                YargLogger.LogFormatInfo("Setlist socket listening on 127.0.0.1:{0}", port);
            }
            catch (SocketException ex)
            {
                _retryAt = Time.unscaledTime + RETRY_SECONDS;
                if (_toastedPort != port)
                {
                    _toastedPort = port;
                    YargLogger.LogException(ex, $"Failed to open the setlist socket on port {port}");
                    ToastManager.ToastError(
                        $"Couldn't open the setlist socket on port {port}. Another app may be using it.");
                }
            }
        }

        private void StopServer()
        {
            if (_server is null)
            {
                return;
            }

            _server.Stop();
            _server = null;
            YargLogger.LogInfo("Setlist socket closed");
        }

        private void PublishIfChanged()
        {
            int setlistRevision = Setlist.Revision;
            bool active = Setlist.IsActive;
            // A rescan or a content-rating change can stop setlist songs resolving without touching the setlist
            int libraryRevision = SongContainer.LibraryRevision;

            if (_published && setlistRevision == _publishedSetlistRevision && active == _publishedActive &&
                libraryRevision == _publishedLibraryRevision)
            {
                return;
            }

            _published = true;
            _publishedSetlistRevision = setlistRevision;
            _publishedActive = active;
            _publishedLibraryRevision = libraryRevision;

            // An inactive setlist is about to be cleared, so clients see it empty
            if (active)
            {
                Setlist.ResolveSongsInto(_songs);
            }
            else
            {
                _songs.Clear();
            }

            var snapshot = _server.PendingSnapshot;
            int staleCount = snapshot.EntryCount;

            snapshot.Active = active;
            snapshot.ShowInProgress = Setlist.IsPlaying;
            snapshot.CurrentIndex = Setlist.Index;
            snapshot.TotalCount = _songs.Count;
            snapshot.EntryCount = Math.Min(_songs.Count, snapshot.Entries.Length);

            for (int i = 0; i < snapshot.EntryCount; i++)
            {
                var song = _songs[i];
                ref var entry = ref snapshot.Entries[i];
                entry.Hash = song.Hash;
                entry.LengthMs = (uint) Math.Clamp(song.SongLengthMilliseconds, 0, uint.MaxValue);
                entry.Title = song.Name.Original;
                entry.Artist = song.Artist.Original;
                entry.Album = song.Album.Original;
            }

            // Don't keep strings of songs that have left the setlist alive
            if (staleCount > snapshot.EntryCount)
            {
                Array.Clear(snapshot.Entries, snapshot.EntryCount, staleCount - snapshot.EntryCount);
            }

            _server.Publish();
        }
    }
}
