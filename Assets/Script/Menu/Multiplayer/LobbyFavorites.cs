using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Networking.Bookmarks;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Facade for lobby bookmark persistence.
    /// </summary>
    public sealed class LobbyFavorites : IDisposable
    {
        private readonly LobbyBookmarkStore _store;

        public event Action OnFavoritesChanged;

        public LobbyFavorites()
        {
            _store = LobbyBookmarkStore.Instance;
            _store.Changed += HandleStoreChanged;
        }

        public void Dispose()
        {
            _store.Changed -= HandleStoreChanged;
        }

        public IReadOnlyList<LobbyBookmark> GetFavorites()
        {
            return _store.Favorites;
        }

        public IReadOnlyList<LobbyBookmark> GetRecents()
        {
            return _store.Recents;
        }

        public LobbyBookmark FindBookmark(string address, int port)
        {
            return _store.GetFavorite(address, port) ?? _store.GetRecent(address, port);
        }

        public bool IsFavorited(string address, int port)
        {
            return _store.IsFavorite(address, port);
        }

        public void AddFavorite(string address, int port, string name, string password)
        {
            _store.AddFavorite(address, port, name, password);
        }

        public void RemoveFavorite(string address, int port)
        {
            _store.RemoveFavorite(address, port);
        }

        public void RecordConnection(string address, int port, string name, string password)
        {
            _store.RecordConnection(address, port, name, password);
        }

        public void UpdateBookmark(LobbyBookmark bookmark, string displayName, string address, int port, string password)
        {
            _store.UpdateBookmark(bookmark, displayName, address, port, password);
        }

        private void HandleStoreChanged()
        {
            OnFavoritesChanged?.Invoke();
        }
    }
}
