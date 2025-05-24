using System;
using System.Linq;
using YARG.Core.Song.Cache;
using YARG.Player;

namespace YARG.Helpers
{
    public class PlayerContext : IPlayerContext
    {
        public Guid GetCurrentPlayerId()
        {
            return PlayerContainer.Players
                .FirstOrDefault(p => !p.Profile.IsBot)?.Profile.Id ?? Guid.Empty;
        }
    }
}