using System.Collections.Generic;
using System.Drawing;

namespace YARG.Core.Game
{
    public partial class HighwayPreset : BasePreset
    {
        public static HighwayPreset Default = new("Default", true);
        public static HighwayPreset Basic = new("Basic", true)
        {
            StarPowerColor = Color.FromArgb(0, 0, 0, 0),
            BackgroundPatternColor = Color.FromArgb(0, 0, 0, 0),
            BackgroundBaseColor2 = Color.FromArgb(0, 0, 0, 0),
            BackgroundGroovePatternColor = Color.FromArgb(0,0,0,0),
            BackgroundGrooveBaseColor1 = Color.FromArgb(255, 15, 15, 15),
            BackgroundGrooveBaseColor2 = Color.FromArgb(0, 0, 0, 0)
        };
        public static HighwayPreset NoGroove = new("No Groove", true)
        {
            BackgroundGroovePatternColor = Color.FromArgb(255, 87, 87, 87),
            BackgroundGrooveBaseColor1 = Color.FromArgb(255, 15, 15, 15),
            BackgroundGrooveBaseColor2 = Color.FromArgb(38, 75, 75, 75)
        };

        public static readonly List<HighwayPreset> Defaults = new()
        {
            Default,
            Basic,
            NoGroove
        };
    }
}