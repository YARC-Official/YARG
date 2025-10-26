using System;
using YARG.Menu.ListMenu;
using YARG.Networking;
using YARG.Networking.Bookmarks;
using Cysharp.Text;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;
using UnityEngine;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Base class for lobby browser view types.
    /// </summary>
    public abstract class LobbyViewType : BaseViewType
    {
        public virtual bool ShowFavoriteButton => false;
        public virtual bool IsFavorited => false;
        public virtual int Ping => -1;
        
        public abstract void OnJoinClick();
        public virtual void OnFavoriteClick() { }
        
        public override void IconClick()
        {
            OnJoinClick();
        }
        
        // BaseViewType requires GetSecondaryText
        public override string GetSecondaryText(bool selected) => string.Empty;
    }
    
    /// <summary>
    /// ViewType for displaying a discovered lobby.
    /// </summary>
    public class DiscoveredLobbyViewType : LobbyViewType
    {
        private readonly YargNetworkManager.LobbyInfo _lobbyInfo;
        private readonly LobbyBrowserMenu _menu;
        private readonly LobbyFavorites _favorites;
        
        public override BackgroundType Background => BackgroundType.Normal;
        public override bool ShowFavoriteButton => true;
        public override bool IsFavorited => _favorites.IsFavorited(_lobbyInfo.ipAddress, _lobbyInfo.port);
        public override int Ping => CalculatePing();
        
        public YargNetworkManager.LobbyInfo LobbyInfo => _lobbyInfo;
        
        public DiscoveredLobbyViewType(YargNetworkManager.LobbyInfo lobbyInfo, LobbyBrowserMenu menu, LobbyFavorites favorites)
        {
            _lobbyInfo = lobbyInfo;
            _menu = menu;
            _favorites = favorites;
        }
        
        public override string GetPrimaryText(bool selected)
        {
            // Show lobby name using proper text formatting
            return FormatAs(_lobbyInfo.lobbyName, TextType.Primary, selected);
        }
        
        public override string GetSecondaryText(bool selected)
        {
            // Show host name using proper text formatting
            return FormatAs($"Host: {_lobbyInfo.hostName}", TextType.Secondary, selected);
        }
        
        public string GetPlayerCountText()
        {
            var filledColor = _lobbyInfo.currentPlayers >= _lobbyInfo.maxPlayers 
                ? new Color(1f, 0.3f, 0.3f) // Red when full
                : MenuData.Colors.PrimaryText;
            
            var currentText = TextColorer.StyleString(
                ZString.Format("{0}", _lobbyInfo.currentPlayers),
                filledColor,
                600);
            
            var maxText = TextColorer.StyleString(
                ZString.Format("/{0}", _lobbyInfo.maxPlayers),
                MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                400);
            
            return ZString.Concat(currentText, maxText);
        }
        
        public string GetPingText()
        {
            int ping = CalculatePing();
            if (ping < 0)
            {
                return TextColorer.StyleString("--", MenuData.Colors.PrimaryText.WithAlpha(0.5f), 400);
            }
            
            // Color based on ping quality
            Color pingColor;
            if (ping < 50)
                pingColor = new Color(0.3f, 1f, 0.3f); // Green
            else if (ping < 100)
                pingColor = new Color(1f, 1f, 0.3f); // Yellow
            else
                pingColor = new Color(1f, 0.3f, 0.3f); // Red
            
            return TextColorer.StyleString(ZString.Format("{0}ms", ping), pingColor, 500);
        }
        
        public bool HasPassword()
        {
            return _lobbyInfo.hasPassword;
        }
        
        private int CalculatePing()
        {
            // Calculate ping based on lastSeen timestamp
            // For now, return a simulated value
            // TODO: Implement proper ping measurement
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long timeSinceLastSeen = currentTime - _lobbyInfo.lastSeen;
            
            // If we haven't seen the lobby in a while, show high ping
            if (timeSinceLastSeen > 5000)
                return 999;
            
            // Otherwise, simulate based on network discovery interval
            return UnityEngine.Random.Range(10, 100);
        }
        
        public override void OnJoinClick()
        {
            _menu.JoinLobby(_lobbyInfo);
        }
        
        public override void OnFavoriteClick()
        {
            if (IsFavorited)
            {
                _favorites.RemoveFavorite(_lobbyInfo.ipAddress, _lobbyInfo.port);
            }
            else
            {
                _favorites.AddFavorite(_lobbyInfo.ipAddress, _lobbyInfo.port, _lobbyInfo.lobbyName, string.Empty);
            }
        }
    }
    
    /// <summary>
    /// ViewType for a category header in the lobby browser.
    /// </summary>
    public class LobbyCategoryViewType : LobbyViewType
    {
        private readonly string _categoryName;
        
        public override BackgroundType Background => BackgroundType.Category;
        
        public LobbyCategoryViewType(string categoryName)
        {
            _categoryName = categoryName;
        }
        
        public override string GetPrimaryText(bool selected)
        {
            // Use bright text for category headers
            return FormatAs(_categoryName, TextType.Bright, selected);
        }
        
        public override string GetSecondaryText(bool selected)
        {
            // Categories don't have secondary text
            return string.Empty;
        }
        
        public override void OnJoinClick()
        {
            // Categories are not joinable
        }
    }

    /// <summary>
    /// ViewType for offline saved bookmarks (favorites or recents).
    /// </summary>
    public class SavedLobbyViewType : LobbyViewType
    {
        private readonly LobbyBookmark _bookmark;
        private readonly LobbyBrowserMenu _menu;
        private readonly LobbyFavorites _favorites;

        public override BackgroundType Background => BackgroundType.Normal;
        public override bool ShowFavoriteButton => true;
        public override bool IsFavorited => _favorites.IsFavorited(_bookmark.address, _bookmark.port);

        public SavedLobbyViewType(LobbyBookmark bookmark, LobbyBrowserMenu menu, LobbyFavorites favorites)
        {
            _bookmark = bookmark;
            _menu = menu;
            _favorites = favorites;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(string.IsNullOrWhiteSpace(_bookmark.displayName) ? _bookmark.address : _bookmark.displayName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            int clampedPort = Math.Clamp(_bookmark.port, 0, ushort.MaxValue);
            var endpoint = string.Concat(_bookmark.address, ":", clampedPort);
            return FormatAs(endpoint, TextType.Secondary, selected);
        }

        public override void OnJoinClick()
        {
            _menu.JoinSavedBookmark(_bookmark);
        }

        public override void OnFavoriteClick()
        {
            if (IsFavorited)
            {
                _favorites.RemoveFavorite(_bookmark.address, _bookmark.port);
            }
            else
            {
                _favorites.AddFavorite(_bookmark.address, _bookmark.port, _bookmark.displayName, _bookmark.password);
            }
        }
    }
}
