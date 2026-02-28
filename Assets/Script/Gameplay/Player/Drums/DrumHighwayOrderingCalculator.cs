using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;

namespace YARG.Gameplay.Player.Drums
{
    public abstract class DrumHighwayOrderingCalculator
    {
        /** Number of frets/display lanes to show. Does not include the kick. **/
        public abstract int FretCount { get; }

        /** True for five-lane drums, false for four-lane and four-lane pro drums. **/
        public abstract bool IsFiveLaneMode { get; }

        /** The list of all pad enum values (eg. FourFretDrumPad). **/
        protected abstract int[] AllPads { get; }

        /** Returns the drum pad enum value (eg. FourFretDrumPad) for the given DrumsAction. **/
        public abstract int GetPad(DrumsAction action);

        /** Returns the 0-based lane position for a given drum pad. **/
        protected abstract int GetPosition(int pad);

        /**
         * Returns the color index for a given drum pad.
         * The first index is the kick color, and the rest are the 1-indexed frets lanes.
         * The int values returned are the ColorProfileIndex values defined in the ColorProfile classes.
         * Note that when LeftyFlip is enabled, this method reverses the colors. This is because the lanes are switched visually,
         * but we still want eg. the red lane to be the first lane, even when the snare is on the right.
         **/
        protected abstract int GetColorIndex(int pad);

        public Dictionary<int, HighwayOrderingInfo> HighwayOrdering { get; private set; }

        protected YargPlayer Player { get; }

        protected DrumHighwayOrderingCalculator(YargPlayer player)
        {
            Player = player;
            HighwayOrdering = GenerateHighwayOrdering();
        }

#region HighwayOrdering
        private Dictionary<int, HighwayOrderingInfo> GenerateHighwayOrdering()
        {
            return AllPads.ToDictionary(pad => pad, GenerateHighwayOrderingInfo);
        }

        private HighwayOrderingInfo GenerateHighwayOrderingInfo(int pad)
        {
            return new(GetPosition(pad), GetColorIndex(pad));
        }

        public HighwayOrderingInfo GetHighwayOrderingInfo(int pad)
        {
            if (HighwayOrdering.TryGetValue(pad, out var info))
            {
                return info;
            }

            return new(-1, pad);
        }
#endregion
    }
}