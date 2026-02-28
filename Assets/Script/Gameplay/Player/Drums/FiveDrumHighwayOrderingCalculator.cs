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
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;

namespace YARG.Gameplay.Player.Drums
{
    public class FiveDrumHighwayOrderingCalculator : DrumHighwayOrderingCalculator
    {
        public override int FretCount => 5;

        public override bool IsFiveLaneMode => true;
        private bool ShouldSwapSnareAndHiHat => Player.Profile.SwapSnareAndHiHat;
        private bool LeftyFlip => Player.Profile.LeftyFlip;
        protected override int[] AllPads => Enum.GetValues(typeof(FiveLaneDrumPad)).Cast<int>().Except(new[] { (int) FiveLaneDrumPad.Kick }).ToArray();

        public FiveDrumHighwayOrderingCalculator(YargPlayer player) : base(player) {}

#region GetPosition methods
        public override int GetPad(DrumsAction action)
        {
            return action switch
            {
                DrumsAction.RedDrum => (int)FiveLaneDrumPad.Red,
                DrumsAction.YellowCymbal => (int)FiveLaneDrumPad.Yellow,
                DrumsAction.BlueDrum => (int)FiveLaneDrumPad.Blue,
                DrumsAction.OrangeCymbal => (int)FiveLaneDrumPad.Orange,
                DrumsAction.GreenDrum => (int)FiveLaneDrumPad.Green,
                _ => -1,
            };
        }

        protected override int GetPosition(int pad)
        {
            int position = pad switch
            {
                (int) FiveLaneDrumPad.Red    => ShouldSwapSnareAndHiHat ? 1 : 0,
                (int) FiveLaneDrumPad.Yellow => ShouldSwapSnareAndHiHat ? 0 : 1,
                (int) FiveLaneDrumPad.Blue   => 2,
                (int) FiveLaneDrumPad.Orange => 3,
                (int) FiveLaneDrumPad.Green  => 4,
                _                            => -1,
            };
            return LeftyFlip ? FretCount - position - 1 : position;
        }
        #endregion

        #region Colors
        protected override int GetColorIndex(int pad)
        {
            var color = (FiveLaneDrumPad)pad switch
            {
                FiveLaneDrumPad.Kick   => ColorProfile.FiveLaneDrumsFret.Kick,
                FiveLaneDrumPad.Red    => ColorProfile.FiveLaneDrumsFret.Red,
                FiveLaneDrumPad.Yellow => ColorProfile.FiveLaneDrumsFret.Yellow,
                FiveLaneDrumPad.Blue   => ColorProfile.FiveLaneDrumsFret.Blue,
                FiveLaneDrumPad.Orange => ColorProfile.FiveLaneDrumsFret.Orange,
                FiveLaneDrumPad.Green  => ColorProfile.FiveLaneDrumsFret.Green,
                _                      => ColorProfile.FiveLaneDrumsFret.Red,
            };
            return LeftyFlip ? (int)UpdateColorForLeftyFlip(color) : (int)color;
        }

        private ColorProfile.FiveLaneDrumsFret UpdateColorForLeftyFlip(ColorProfile.FiveLaneDrumsFret color)
        {
            // When LeftyMode is enabled, the lanes are switched visually, but not internally.
            // This leads to the colors being wrong, so we need to swap them around.
            return color switch
            {
                ColorProfile.FiveLaneDrumsFret.Red    => ColorProfile.FiveLaneDrumsFret.Green,
                ColorProfile.FiveLaneDrumsFret.Yellow => ColorProfile.FiveLaneDrumsFret.Orange,
                ColorProfile.FiveLaneDrumsFret.Orange => ColorProfile.FiveLaneDrumsFret.Yellow,
                ColorProfile.FiveLaneDrumsFret.Green  => ColorProfile.FiveLaneDrumsFret.Red,
                _                                     => color,
            };
        }
#endregion
    }
}