using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Networking;

namespace YARG.Networking.Bookmarks
{
    /// <summary>
    /// Persists lobby bookmarks (favorites + recents) to disk.
    /// </summary>
    public sealed class LobbyBookmarkStore
    {
        private const string StorageFileName = "lobby_bookmarks.json";
        private const int MaxRecentEntries = 25;

        private static LobbyBookmarkStore _instance;

        private readonly List<LobbyBookmark> _favorites = new();
        private readonly List<LobbyBookmark> _recents = new();
        private readonly List<HostedLobbyPreset> _myLobbies = new();

        private readonly string _storagePath;

        public static LobbyBookmarkStore Instance => _instance ??= new LobbyBookmarkStore();

        public IReadOnlyList<LobbyBookmark> Favorites => _favorites;
        public IReadOnlyList<LobbyBookmark> Recents => _recents;
        public IReadOnlyList<HostedLobbyPreset> MyLobbies => _myLobbies;

        public event Action Changed;

        private LobbyBookmarkStore()
        {
            _storagePath = Path.Combine(Application.persistentDataPath, StorageFileName);
            Load();
        }

        public bool IsFavorite(string address, int port)
        {
            return _favorites.Any(entry => LobbyBookmarkUtility.Matches(address, port, entry));
        }

        public LobbyBookmark GetFavorite(string address, int port)
        {
            return _favorites.FirstOrDefault(entry => LobbyBookmarkUtility.Matches(address, port, entry));
        }

        public LobbyBookmark GetRecent(string address, int port)
        {
            return _recents.FirstOrDefault(entry => LobbyBookmarkUtility.Matches(address, port, entry));
        }

        public void ToggleFavorite(LobbyBookmark bookmark)
        {
            if (bookmark == null)
            {
                return;
            }

            if (IsFavorite(bookmark.address, bookmark.port))
            {
                RemoveFavorite(bookmark.address, bookmark.port);
                return;
            }

            AddFavorite(bookmark.address, bookmark.port, bookmark.displayName, bookmark.password);
        }

        public void AddFavorite(string address, int port, string displayName, string password)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            var existing = GetFavorite(address, port);
            if (existing != null)
            {
                if (!existing.displayNamePinned && !string.IsNullOrWhiteSpace(displayName))
                {
                    existing.displayName = displayName.Trim();
                }
                existing.password = password ?? string.Empty;
                existing.lastConnected = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                _favorites.Add(new LobbyBookmark
                {
                    address = address.Trim(),
                    port = port,
                    displayName = string.IsNullOrWhiteSpace(displayName) ? address : displayName.Trim(),
                    password = password ?? string.Empty,
                    favorite = true,
                    createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    lastConnected = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    displayNamePinned = false
                });
            }

            Save();
            Changed?.Invoke();
            // Keep favorites sorted by when they were added (most recent first)
            SortFavorites();
        }

        public void RemoveFavorite(string address, int port)
        {
            // Demote favorite to recents instead of deleting so user still has the entry available
            DemoteFavoriteToRecent(address, port);
        }

        private void DemoteFavoriteToRecent(string address, int port)
        {
            var fav = GetFavorite(address, port);
            if (fav == null)
                return;

            // Create or update recent entry from favorite
            var existingRecent = GetRecent(address, port);
            if (existingRecent != null)
            {
                if (!existingRecent.displayNamePinned && !string.IsNullOrWhiteSpace(fav.displayName))
                {
                    existingRecent.displayName = fav.displayName;
                }
                existingRecent.displayNamePinned = existingRecent.displayNamePinned || fav.displayNamePinned;
                existingRecent.password = fav.password ?? existingRecent.password;
                existingRecent.lastConnected = fav.lastConnected;
            }
            else
            {
                var recent = new LobbyBookmark
                {
                    address = fav.address,
                    port = fav.port,
                    displayName = string.IsNullOrWhiteSpace(fav.displayName) ? fav.address : fav.displayName,
                    password = fav.password ?? string.Empty,
                    favorite = false,
                    createdAt = fav.createdAt,
                    lastConnected = fav.lastConnected,
                    displayNamePinned = fav.displayNamePinned
                };

                _recents.Insert(0, recent);
            }

            // Remove favorite entry
            bool removed = _favorites.RemoveAll(entry => LobbyBookmarkUtility.Matches(address, port, entry)) > 0;

            // Ensure recents list size cap and sorting
            if (_recents.Count > MaxRecentEntries)
            {
                _recents.RemoveRange(MaxRecentEntries, _recents.Count - MaxRecentEntries);
            }

            SortRecents();
            SortFavorites();

            if (removed)
            {
                Save();
                Changed?.Invoke();
            }
        }

        public void UpdateBookmark(LobbyBookmark bookmark, string displayName, string address, int port, string password)
        {
            if (bookmark == null)
            {
                return;
            }

            string trimmedAddress = string.IsNullOrWhiteSpace(address) ? bookmark.address : address.Trim();
            if (string.IsNullOrWhiteSpace(trimmedAddress))
            {
                return;
            }

            int fallbackPort = bookmark.port > 0 ? bookmark.port : NetworkTransportDefaults.DefaultUdpPort;
            int normalizedPort = Math.Clamp(port > 0 ? port : fallbackPort, 1, ushort.MaxValue);
            string normalizedName = string.IsNullOrWhiteSpace(displayName) ? trimmedAddress : displayName.Trim();
            string normalizedPassword = password ?? string.Empty;

            string originalKey = bookmark.EndpointKey;

            bookmark.displayNamePinned = true;

            bool changed = false;

            changed |= ApplyBookmarkUpdate(_favorites, originalKey, normalizedName, trimmedAddress, normalizedPort, normalizedPassword, true, true);
            changed |= ApplyBookmarkUpdate(_recents, originalKey, normalizedName, trimmedAddress, normalizedPort, normalizedPassword, false, true);

            if (changed)
            {
                Save();
                Changed?.Invoke();
                // Keep lists sorted after an update
                SortRecents();
                SortFavorites();
            }
        }

        public void RecordConnection(string address, int port, string displayName, string password)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            if (port <= 0)
            {
                port = NetworkTransportDefaults.DefaultTcpPort;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var key = LobbyBookmarkUtility.BuildKey(address, port);

            // Update recents
            var existingRecent = _recents.FirstOrDefault(entry => entry.EndpointKey == key);
            if (existingRecent != null)
            {
                if (!existingRecent.displayNamePinned && !string.IsNullOrWhiteSpace(displayName))
                {
                    existingRecent.displayName = displayName.Trim();
                }
                existingRecent.password = password ?? existingRecent.password;
                existingRecent.lastConnected = timestamp;
            }
            else
            {
                var recent = new LobbyBookmark
                {
                    address = address.Trim(),
                    port = port,
                    displayName = string.IsNullOrWhiteSpace(displayName) ? address : displayName.Trim(),
                    password = password ?? string.Empty,
                    favorite = false,
                    createdAt = timestamp,
                    lastConnected = timestamp,
                    displayNamePinned = false
                };

                _recents.Insert(0, recent);
            }

            // Cap recents list size
            if (_recents.Count > MaxRecentEntries)
            {
                _recents.RemoveRange(MaxRecentEntries, _recents.Count - MaxRecentEntries);
            }

            // Update corresponding favorite timestamp
            var favorite = GetFavorite(address, port);
            if (favorite != null)
            {
                favorite.lastConnected = timestamp;
                if (!favorite.displayNamePinned && !string.IsNullOrWhiteSpace(displayName))
                {
                    favorite.displayName = displayName.Trim();
                }
                favorite.password = password ?? favorite.password;
            }

            Save();
            Debug.Log($"[LobbyBookmarkStore] Recorded connection to {address}:{port} (favorite: {favorite != null})");
            Changed?.Invoke();
        }

        /// <summary>
        /// Promote a recent entry to a favorite. If a favorite for the same address/port
        /// already exists it will be updated. Removes matching recents after promotion.
        /// </summary>
        public void PromoteRecentToFavorite(string address, int port, string displayName, string password)
        {
            if (string.IsNullOrWhiteSpace(address))
                return;

            string normalizedName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            var recent = GetRecent(address, port);
            bool shouldPinDisplayName = recent != null && (recent.displayNamePinned || (!string.IsNullOrWhiteSpace(normalizedName) && !string.Equals(recent.displayName, normalizedName, StringComparison.Ordinal)));

            if (recent != null && shouldPinDisplayName)
            {
                recent.displayNamePinned = true;
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    recent.displayName = normalizedName;
                }
            }

            // Create or update favorite
            AddFavorite(address, port, normalizedName, password);

            var favorite = GetFavorite(address, port);
            if (favorite != null && shouldPinDisplayName)
            {
                favorite.displayNamePinned = true;
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    favorite.displayName = normalizedName;
                }
            }

            // Remove any recents that match this endpoint
            int removed = _recents.RemoveAll(entry => LobbyBookmarkUtility.Matches(address, port, entry));
            if (removed > 0)
            {
                Save();
                Changed?.Invoke();
            }
        }

        public HostedLobbyPreset UpsertMyLobby(string id, string lobbyName, int maxPlayers, YargNetworkManager.LobbyPrivacyMode privacyMode, string password, bool updateHostedTimestamp)
        {
            string normalizedName = string.IsNullOrWhiteSpace(lobbyName) ? "My Lobby" : lobbyName.Trim();
            int clampedPlayers = Mathf.Clamp(maxPlayers, 2, 32);
            string normalizedPassword = privacyMode == YargNetworkManager.LobbyPrivacyMode.Private ? (password ?? string.Empty) : string.Empty;

            HostedLobbyPreset preset = null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                preset = _myLobbies.FirstOrDefault(p => string.Equals(p.id, id, StringComparison.Ordinal));
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (preset == null)
            {
                preset = new HostedLobbyPreset
                {
                    id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim(),
                    lobbyName = normalizedName,
                    maxPlayers = clampedPlayers,
                    privacyMode = (int)privacyMode,
                    password = normalizedPassword,
                    createdAt = now,
                    lastHostedAt = updateHostedTimestamp ? now : 0
                };

                _myLobbies.Insert(0, preset);
            }
            else
            {
                preset.lobbyName = normalizedName;
                preset.maxPlayers = clampedPlayers;
                preset.PrivacyMode = privacyMode;
                preset.password = normalizedPassword;

                if (updateHostedTimestamp)
                {
                    preset.TouchHostedTimestamp();
                }
            }

            SortMyLobbies();
            Save();
            Changed?.Invoke();

            return preset;
        }

        public HostedLobbyPreset GetMyLobby(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return _myLobbies.FirstOrDefault(p => string.Equals(p.id, id, StringComparison.Ordinal));
        }

        public void TouchMyLobbyHosted(string id)
        {
            var preset = GetMyLobby(id);
            if (preset == null)
                return;

            preset.TouchHostedTimestamp();
            SortMyLobbies();
            Save();
            Changed?.Invoke();
        }

        public bool RemoveMyLobby(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            int removed = _myLobbies.RemoveAll(p => string.Equals(p.id, id, StringComparison.Ordinal));
            if (removed > 0)
            {
                Save();
                Changed?.Invoke();
                return true;
            }

            return false;
        }

        private void Load()
        {
            _favorites.Clear();
            _recents.Clear();
            _myLobbies.Clear();

            if (!File.Exists(_storagePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_storagePath);
                var payload = JsonUtility.FromJson<BookmarkData>(json) ?? new BookmarkData();

                if (payload.favorites != null)
                {
                    _favorites.AddRange(payload.favorites.Where(entry => !string.IsNullOrWhiteSpace(entry.address)));
                }

                if (payload.recents != null)
                {
                    _recents.AddRange(payload.recents.Where(entry => !string.IsNullOrWhiteSpace(entry.address)));
                }

                if (payload.myLobbies != null)
                {
                    foreach (var preset in payload.myLobbies)
                    {
                        if (preset == null)
                            continue;

                        preset.EnsureIdentifiers();
                        preset.maxPlayers = Mathf.Clamp(preset.maxPlayers, 2, 32);

                        if (string.IsNullOrWhiteSpace(preset.lobbyName))
                        {
                            preset.lobbyName = "My Lobby";
                        }

                        _myLobbies.Add(preset);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBookmarkStore] Failed to load bookmarks: {ex.Message}");
            }

            SortMyLobbies();
        }

        private void Save()
        {
            try
            {
                var payload = new BookmarkData
                {
                    favorites = _favorites.Select(entry => entry.Clone()).ToList(),
                    recents = _recents.Select(entry => entry.Clone()).ToList(),
                    myLobbies = _myLobbies.Select(entry => entry.Clone()).ToList()
                };

                var json = JsonUtility.ToJson(payload, true);
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBookmarkStore] Failed to save bookmarks: {ex.Message}");
            }
        }

        private void SortRecents()
        {
            if (_recents == null) return;
            _recents.Sort((a, b) => b.lastConnected.CompareTo(a.lastConnected));
        }

        private void SortFavorites()
        {
            if (_favorites == null) return;
            // Sort favorites by createdAt ascending (oldest first) so UI can show oldest at top
            _favorites.Sort((a, b) => a.createdAt.CompareTo(b.createdAt));
        }

        private void SortMyLobbies()
        {
            if (_myLobbies == null) return;
            _myLobbies.Sort((a, b) =>
            {
                int hostedCompare = b.lastHostedAt.CompareTo(a.lastHostedAt);
                if (hostedCompare != 0)
                    return hostedCompare;

                return b.createdAt.CompareTo(a.createdAt);
            });
        }

        /// <summary>
        /// Return favorites ordered with online entries first (based on provided endpoint keys), then by createdAt desc.
        /// </summary>
        public IReadOnlyList<LobbyBookmark> GetFavoritesOrderedByOnline(IEnumerable<string> onlineEndpointKeys)
        {
            var onlineSet = new HashSet<string>(onlineEndpointKeys ?? System.Array.Empty<string>());
            // Online entries first, then sort within each group by createdAt ascending (oldest -> newest)
            var ordered = _favorites.OrderByDescending(b => onlineSet.Contains(b.EndpointKey))
                                    .ThenBy(b => b.createdAt)
                                    .ToList();
            return ordered;
        }

        [Serializable]
        private sealed class BookmarkData
        {
            public List<LobbyBookmark> favorites;
            public List<LobbyBookmark> recents;
            public List<HostedLobbyPreset> myLobbies;
        }

        private static bool ApplyBookmarkUpdate(List<LobbyBookmark> list, string originalKey,
            string displayName, string address, int port, string password, bool favoriteFlag, bool? pinDisplayName)
        {
            if (list == null || list.Count == 0)
            {
                return false;
            }

            int index = list.FindIndex(entry => entry.EndpointKey == originalKey);
            if (index < 0)
            {
                return false;
            }

            var entry = list[index];
            entry.displayName = displayName;
            entry.address = address;
            entry.port = port;
            entry.password = password;
            entry.favorite = favoriteFlag;
            if (pinDisplayName.HasValue)
            {
                entry.displayNamePinned = pinDisplayName.Value;
            }
            return true;
        }
    }
}
