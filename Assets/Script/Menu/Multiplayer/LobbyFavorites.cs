using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Helpers;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Manages favorited lobby IP addresses.
    /// Allows users to mark lobbies they want to easily find again.
    /// </summary>
    [Serializable]
    public class LobbyFavorites
    {
        [Serializable]
        private class FavoriteEntry
        {
            public string ipAddress;
            public string displayName;
            public long lastConnected;
        }
        
        [Serializable]
        private class FavoritesData
        {
            public List<FavoriteEntry> favorites = new List<FavoriteEntry>();
        }
        
        private const string FAVORITES_FILENAME = "lobby_favorites.json";
        
        private FavoritesData _data;
        private string _favoritesPath;
        private bool _initialized;
        
        public event Action OnFavoritesChanged;
        
        public LobbyFavorites()
        {
            _data = new FavoritesData();
        }
        
        private void EnsureInitialized()
        {
            if (_initialized) return;
            
            _favoritesPath = Path.Combine(Application.persistentDataPath, FAVORITES_FILENAME);
            Load();
            _initialized = true;
        }
        
        /// <summary>
        /// Checks if an IP address is favorited.
        /// </summary>
        public bool IsFavorited(string ipAddress)
        {
            EnsureInitialized();
            return _data.favorites.Any(f => f.ipAddress == ipAddress);
        }
        
        /// <summary>
        /// Adds a lobby to favorites.
        /// </summary>
        public void AddFavorite(string ipAddress, string displayName)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(ipAddress))
                return;
            
            // Don't add duplicates
            if (IsFavorited(ipAddress))
                return;
            
            var entry = new FavoriteEntry
            {
                ipAddress = ipAddress,
                displayName = displayName,
                lastConnected = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            _data.favorites.Add(entry);
            Save();
            OnFavoritesChanged?.Invoke();
        }
        
        /// <summary>
        /// Removes a lobby from favorites.
        /// </summary>
        public void RemoveFavorite(string ipAddress)
        {
            EnsureInitialized();
            int removed = _data.favorites.RemoveAll(f => f.ipAddress == ipAddress);
            if (removed > 0)
            {
                Save();
                OnFavoritesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Updates the last connected time for a favorite.
        /// </summary>
        public void UpdateLastConnected(string ipAddress)
        {
            EnsureInitialized();
            var entry = _data.favorites.FirstOrDefault(f => f.ipAddress == ipAddress);
            if (entry != null)
            {
                entry.lastConnected = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Save();
            }
        }
        
        /// <summary>
        /// Gets all favorited IP addresses.
        /// </summary>
        public List<string> GetFavoriteIPs()
        {
            EnsureInitialized();
            return _data.favorites.Select(f => f.ipAddress).ToList();
        }
        
        /// <summary>
        /// Gets favorite display name for an IP, or returns the IP if not found.
        /// </summary>
        public string GetDisplayName(string ipAddress)
        {
            EnsureInitialized();
            var entry = _data.favorites.FirstOrDefault(f => f.ipAddress == ipAddress);
            return entry?.displayName ?? ipAddress;
        }
        
        private void Load()
        {
            _data = new FavoritesData();
            
            if (File.Exists(_favoritesPath))
            {
                try
                {
                    string json = File.ReadAllText(_favoritesPath);
                    _data = JsonUtility.FromJson<FavoritesData>(json) ?? new FavoritesData();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load lobby favorites: {e.Message}");
                    _data = new FavoritesData();
                }
            }
        }
        
        private void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(_favoritesPath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save lobby favorites: {e.Message}");
            }
        }
    }
}
