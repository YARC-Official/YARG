using System;
using System.Linq;
using YARG.Core;
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

        public Instrument GetCurrentInstrument()
        {
            return PlayerContainer.Players.FirstOrDefault(p => !p.Profile.IsBot)?.Profile.CurrentInstrument
                ?? Instrument.FiveFretGuitar; // fallback on Five Fret Guitar as default 
        }
        
        public Difficulty GetCurrentDifficulty()
        {
            return PlayerContainer.Players.FirstOrDefault(p => !p.Profile.IsBot)?.Profile.CurrentDifficulty 
                ?? Difficulty.Expert; // fallback on Expert as default
        }
    }
}