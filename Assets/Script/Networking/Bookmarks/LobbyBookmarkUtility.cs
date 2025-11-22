using System;

namespace YARG.Networking.Bookmarks
{
    public static class LobbyBookmarkUtility
    {
        public static string BuildKey(string address, int port)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            var trimmed = address.Trim();
            return string.Concat(trimmed.ToLowerInvariant(), ":", port);
        }

        public static bool Matches(string address, int port, LobbyBookmark bookmark)
        {
            if (bookmark == null)
            {
                return false;
            }

            return string.Equals(BuildKey(address, port), bookmark.EndpointKey, StringComparison.Ordinal);
        }
    }
}
