using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core;
using YARG.Themes;

namespace YARG.Assets.Script.Helpers
{
    public static class VisualStyleHelpers
    {
        public static VisualStyle GetVisualStyle(GameMode gameMode, Instrument instrument)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar => VisualStyle.FiveFretGuitar,
                GameMode.SixFretGuitar => VisualStyle.SixFretGuitar,
                GameMode.FourLaneDrums => VisualStyle.FourLaneDrums,
                GameMode.FiveLaneDrums => VisualStyle.FiveLaneDrums,
                GameMode.Keys => instrument is Instrument.ProKeys ? VisualStyle.ProKeys : VisualStyle.FiveLaneKeys,
                _ => throw new Exception("Unhandled.")
            };
        }
    }
}
