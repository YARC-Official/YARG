using System;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Scores;

namespace YARG.Helpers
{
    public class StarProvider : IStarProvider
    {
        public StarAmount GetBestStarsForSong(HashWrapper songHash, Guid playerId, Instrument instrument, Difficulty difficulty)
        {
            return ScoreContainer.GetBestStarsForSong(songHash, playerId, instrument, difficulty);
        }
    }
}