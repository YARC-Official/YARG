using System;
using System.Globalization;
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
        public virtual bool CanEdit => false;
        internal virtual LobbyBrowserMenu MenuOwner => null;
        
        public abstract void OnJoinClick();
        public virtual void OnFavoriteClick() { }
        public virtual void OnEditClick() { }
        
        public override void IconClick()
        {
            OnJoinClick();
        }
        
        // BaseViewType requires GetSecondaryText
        public override string GetSecondaryText(bool selected) => string.Empty;

        /// <summary>
        /// Returns a stable selection key identifying this view across list rebuilds.
        /// Used by LobbyBrowserMenu to preserve selection when the view list is refreshed.
        /// </summary>
        public virtual string GetSelectionKey()
        {
            return null;
        }
    }
    
    /// <summary>
    /// ViewType for displaying a discovered lobby.
    /// </summary>
    public class DiscoveredLobbyViewType : LobbyViewType
    {
        private readonly YargNetworkManager.LobbyInfo _lobbyInfo;
        private readonly LobbyBrowserMenu _menu;
        private readonly LobbyFavorites _favorites;
        
        internal override LobbyBrowserMenu MenuOwner => _menu;

        public override BackgroundType Background => BackgroundType.Normal;
        public override bool ShowFavoriteButton => true;
        public override bool IsFavorited => _favorites.IsFavorited(_lobbyInfo.ipAddress, _lobbyInfo.port);
        public override int Ping
        {
            get
            {
                long diff = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lobbyInfo.lastSeen);
                return (int) Math.Min(int.MaxValue, diff);
            }
        }
        public override bool CanEdit => GetBookmark() != null;
        
        public YargNetworkManager.LobbyInfo LobbyInfo => _lobbyInfo;

        public override string GetSelectionKey()
        {
            if (_lobbyInfo == null) return null;
            return string.Concat("disc:", _lobbyInfo.ipAddress, ":", _lobbyInfo.port);
        }
        
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
            if (_lobbyInfo.lastSeen <= 0)
            {
                return TextColorer.StyleString("Awaiting signal", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 400);
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long deltaMs = Math.Max(0, now - _lobbyInfo.lastSeen);
            var delta = TimeSpan.FromMilliseconds(deltaMs);

            string label;
            Color color;

            if (delta <= TimeSpan.FromSeconds(2))
            {
                label = "Live";
                color = new Color(0.35f, 0.92f, 0.55f);
            }
            else if (delta <= TimeSpan.FromSeconds(10))
            {
                int seconds = Mathf.Max(1, Mathf.RoundToInt((float) delta.TotalSeconds));
                label = ZString.Format("{0}s ago", seconds);
                color = new Color(0.96f, 0.78f, 0.32f);
            }
            else if (delta < TimeSpan.FromMinutes(1))
            {
                int seconds = Mathf.Max(10, Mathf.RoundToInt((float) delta.TotalSeconds));
                label = ZString.Format("{0}s ago", seconds);
                color = new Color(0.96f, 0.58f, 0.32f);
            }
            else
            {
                int minutes = Mathf.Max(1, Mathf.RoundToInt((float) delta.TotalMinutes));
                label = minutes == 1 ? "1 min ago" : ZString.Format("{0} mins ago", minutes);
                color = MenuData.Colors.PrimaryText.WithAlpha(0.55f);
            }

            return TextColorer.StyleString(label, color, 500);
        }
        
        public bool HasPassword()
        {
            return _lobbyInfo.hasPassword;
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

        public override void OnEditClick()
        {
            var bookmark = GetBookmark();
            if (bookmark != null)
            {
                _menu.EditBookmark(bookmark);
            }
        }

        private LobbyBookmark GetBookmark()
        {
            return _favorites.FindBookmark(_lobbyInfo.ipAddress, _lobbyInfo.port);
        }
    }
    
    /// <summary>
    /// ViewType for a category header in the lobby browser.
    /// </summary>
    public class LobbyCategoryViewType : LobbyViewType
    {
        private readonly string _categoryName;
        
        public override BackgroundType Background => BackgroundType.Category;

        public string CategoryName => _categoryName;
        
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

        public override string GetSelectionKey()
        {
            return string.Concat("category:", _categoryName ?? string.Empty);
        }
    }

    /// <summary>
    /// Action view that triggers sidebar workflows (create lobby, direct connect, etc.).
    /// </summary>
    public class LobbyActionViewType : LobbyViewType
    {
        public enum ActionKind
        {
            CreateLobby,
            DirectConnect
        }

        private readonly LobbyBrowserMenu _menu;
        private readonly ActionKind _kind;
        private readonly string _title;
        private readonly string _subtitle;

        internal override LobbyBrowserMenu MenuOwner => _menu;

        public override BackgroundType Background => BackgroundType.Normal;

        public LobbyActionViewType(ActionKind kind, LobbyBrowserMenu menu, string title, string subtitle)
        {
            _menu = menu;
            _kind = kind;
            _title = title ?? string.Empty;
            _subtitle = subtitle ?? string.Empty;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_title, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            if (string.IsNullOrEmpty(_subtitle))
                return string.Empty;

            return FormatAs(_subtitle, TextType.Secondary, selected);
        }

        public override void OnJoinClick()
        {
            _menu?.HandleActionSelection(this);
        }

        public ActionKind Kind => _kind;

        public override string GetSelectionKey()
        {
            return string.Concat("action:", _kind.ToString());
        }
    }

    /// <summary>
    /// ViewType for an empty placeholder row in the lobby browser.
    /// </summary>
    public class LobbyEmptyViewType : LobbyViewType
    {
        private readonly string _message;

        public override BackgroundType Background => BackgroundType.Normal;

        public LobbyEmptyViewType(string message)
        {
            _message = message ?? string.Empty;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(_message, TextType.Secondary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return string.Empty;
        }

        public override void OnJoinClick()
        {
            // Not joinable
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

        internal override LobbyBrowserMenu MenuOwner => _menu;

        // Live info from discovery/ping for this saved bookmark (nullable)
        public YargNetworkManager.LobbyInfo LiveInfo { get; set; }

        public override BackgroundType Background => BackgroundType.Normal;
        public override bool ShowFavoriteButton => true;
        public override bool IsFavorited => _favorites.IsFavorited(_bookmark.address, _bookmark.port);
        public override bool CanEdit => true;

        public SavedLobbyViewType(LobbyBookmark bookmark, LobbyBrowserMenu menu, LobbyFavorites favorites)
        {
            _bookmark = bookmark;
            _menu = menu;
            _favorites = favorites;
        }

        // Expose the underlying bookmark so menus/sidebars can show offline bookmark details.
        public LobbyBookmark Bookmark => _bookmark;

        public override string GetSelectionKey()
        {
            if (_bookmark == null) return null;
            return string.Concat("saved:", _bookmark.EndpointKey ?? string.Empty);
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(string.IsNullOrWhiteSpace(_bookmark.displayName) ? _bookmark.address : _bookmark.displayName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            string detail = GetDetailLine();
            return FormatAs(detail, TextType.Secondary, selected);
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

        public override void OnEditClick()
        {
            _menu.EditBookmark(_bookmark);
        }

        public string GetStatusBadge()
        {
            if (LiveInfo != null)
            {
                // Show player count when live
                var filledColor = LiveInfo.currentPlayers >= LiveInfo.maxPlayers
                    ? new Color(1f, 0.3f, 0.3f)
                    : MenuData.Colors.PrimaryText;

                var currentText = TextColorer.StyleString(
                    ZString.Format("{0}", LiveInfo.currentPlayers),
                    filledColor,
                    600);

                var maxText = TextColorer.StyleString(
                    ZString.Format("/{0}", LiveInfo.maxPlayers),
                    MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                    400);

                return ZString.Concat(currentText, maxText);
            }

            return TextColorer.StyleString("OFFLINE", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 600);
        }

        public string GetInfoBadge()
        {
            if (LiveInfo != null)
            {
                // Use discovery lastSeen to show Live/age similar to discovered view
                if (LiveInfo.lastSeen <= 0)
                {
                    return TextColorer.StyleString("Awaiting signal", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 400);
                }

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long deltaMs = Math.Max(0, now - LiveInfo.lastSeen);
                var delta = TimeSpan.FromMilliseconds(deltaMs);

                string label;
                Color color;

                if (delta <= TimeSpan.FromSeconds(2))
                {
                    label = "ONLINE";
                    color = new Color(0.35f, 0.92f, 0.55f);
                }
                else if (delta <= TimeSpan.FromSeconds(10))
                {
                    int seconds = Mathf.Max(1, Mathf.RoundToInt((float) delta.TotalSeconds));
                    label = ZString.Format("{0}s ago", seconds);
                    color = new Color(0.96f, 0.78f, 0.32f);
                }
                else if (delta < TimeSpan.FromMinutes(1))
                {
                    int seconds = Mathf.Max(10, Mathf.RoundToInt((float) delta.TotalSeconds));
                    label = ZString.Format("{0}s ago", seconds);
                    color = new Color(0.96f, 0.58f, 0.32f);
                }
                else
                {
                    int minutes = Mathf.Max(1, Mathf.RoundToInt((float) delta.TotalMinutes));
                    label = minutes == 1 ? "1 min ago" : ZString.Format("{0} mins ago", minutes);
                    color = MenuData.Colors.PrimaryText.WithAlpha(0.55f);
                }

                return TextColorer.StyleString(label, color, 400);
            }

            if (_bookmark.lastConnected <= 0)
            {
                return TextColorer.StyleString("Press Edit to update connection details", MenuData.Colors.PrimaryText.WithAlpha(0.55f), 400);
            }

            var last = DateTimeOffset.FromUnixTimeSeconds(_bookmark.lastConnected).ToLocalTime();
            string relative = BuildRelativeTimeString(last);
            return TextColorer.StyleString(relative, MenuData.Colors.PrimaryText.WithAlpha(0.55f), 400);
        }

        private string GetDetailLine()
        {
            if (_bookmark.lastConnected <= 0)
            {
                return "Saved connection";
            }

            var last = DateTimeOffset.FromUnixTimeSeconds(_bookmark.lastConnected).ToLocalTime();
            string relative = BuildRelativeTimeString(last);
            return string.Concat("Last connected ", relative);
        }

        private static string BuildRelativeTimeString(DateTimeOffset last)
        {
            var now = DateTimeOffset.Now;
            var delta = now - last;

            if (delta < TimeSpan.FromMinutes(1))
            {
                return "just now";
            }

            if (delta < TimeSpan.FromHours(1))
            {
                int minutes = Math.Max(1, (int)Math.Round(delta.TotalMinutes));
                return minutes == 1 ? "1 minute ago" : string.Concat(minutes, " minutes ago");
            }

            if (delta < TimeSpan.FromDays(1))
            {
                int hours = Math.Max(1, (int)Math.Round(delta.TotalHours));
                return hours == 1 ? "1 hour ago" : string.Concat(hours, " hours ago");
            }

            if (delta < TimeSpan.FromDays(7))
            {
                int days = Math.Max(1, (int)Math.Round(delta.TotalDays));
                return days == 1 ? "1 day ago" : string.Concat(days, " days ago");
            }

            return last.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// ViewType for lobbies created and saved by the local player.
    /// </summary>
    public class MyLobbyViewType : LobbyViewType
    {
        private readonly HostedLobbyPreset _preset;
        private readonly LobbyBrowserMenu _menu;

        internal override LobbyBrowserMenu MenuOwner => _menu;

        public MyLobbyViewType(HostedLobbyPreset preset, LobbyBrowserMenu menu)
        {
            _preset = preset;
            _menu = menu;
        }

        public HostedLobbyPreset Preset => _preset;

        public override BackgroundType Background => BackgroundType.Normal;
        public override bool CanEdit => true;

        public override string GetSelectionKey()
        {
            if (_preset == null || string.IsNullOrEmpty(_preset.id))
            {
                return null;
            }

            return string.Concat("mylobby:", _preset.id);
        }

        public override string GetPrimaryText(bool selected)
        {
            string title = _preset?.lobbyName;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "My Lobby";
            }

            return FormatAs(title, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            if (_preset == null)
            {
                return string.Empty;
            }

            string privacy = _preset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private ? "Private" : "Public";

            string hosted = _preset.lastHostedAt > 0
                ? BuildRelativeTimeString(_preset.lastHostedAt)
                : "Never hosted";

            string detail = string.Concat("Max ", _preset.maxPlayers, " · ", privacy, " · ", hosted);
            return FormatAs(detail, TextType.Secondary, selected);
        }

        public override void OnJoinClick()
        {
            if (_preset == null)
                return;

            _menu?.StartHostedLobby(_preset);
        }

        public override void OnEditClick()
        {
            if (_preset == null)
                return;

            _menu?.ShowHostedLobbyEditor(_preset);
        }

        public string GetMaxPlayersLabel(bool selected)
        {
            if (_preset == null)
            {
                return string.Empty;
            }

            int clamped = Mathf.Max(1, _preset.maxPlayers);
            string label = clamped == 1 ? "Max 1 player" : string.Concat("Max ", clamped, " players");
            return FormatAs(label, TextType.Secondary, selected);
        }

        public string GetPrivacyLabel()
        {
            if (_preset == null)
            {
                return string.Empty;
            }

            return _preset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private ? "Private" : "Public";
        }

        public bool IsPasswordProtected()
        {
            if (_preset == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_preset.password))
            {
                return true;
            }

            return _preset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private;
        }

        public string GetHostedRecencyText(bool selected)
        {
            if (_preset == null)
            {
                return string.Empty;
            }

            string recency = _preset.lastHostedAt > 0 ? BuildRelativeTimeString(_preset.lastHostedAt) : "Never hosted";
            return FormatAs(recency, TextType.Secondary, selected);
        }

        private static string BuildRelativeTimeString(long unixSeconds)
        {
            if (unixSeconds <= 0)
            {
                return "Never hosted";
            }

            var last = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
            var delta = DateTimeOffset.Now - last;

            if (delta < TimeSpan.FromMinutes(1))
            {
                return "Hosted just now";
            }

            if (delta < TimeSpan.FromHours(1))
            {
                int minutes = Math.Max(1, (int)Math.Round(delta.TotalMinutes));
                return minutes == 1 ? "Hosted 1 minute ago" : string.Concat("Hosted ", minutes, " minutes ago");
            }

            if (delta < TimeSpan.FromDays(1))
            {
                int hours = Math.Max(1, (int)Math.Round(delta.TotalHours));
                return hours == 1 ? "Hosted 1 hour ago" : string.Concat("Hosted ", hours, " hours ago");
            }

            if (delta < TimeSpan.FromDays(7))
            {
                int days = Math.Max(1, (int)Math.Round(delta.TotalDays));
                return days == 1 ? "Hosted 1 day ago" : string.Concat("Hosted ", days, " days ago");
            }

            return string.Concat("Hosted ", last.ToString("MMM d, yyyy", CultureInfo.CurrentCulture));
        }
    }
}
