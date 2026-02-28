using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
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
using FourLaneDrumsFret = YARG.Core.Game.ColorProfile.FourLaneDrumsFret;

namespace YARG.Gameplay.Player.Drums
{
    public class FourDrumHighwayOrderingCalculator : DrumHighwayOrderingCalculator
    {
        public override bool IsFiveLaneMode => false;
        private bool IsSplitMode => Player.Profile.CurrentInstrument is Instrument.ProDrums && Player.Profile.SplitProTomsAndCymbals;
        private bool ShouldSwapSnareAndHiHat => IsSplitMode && Player.Profile.SwapSnareAndHiHat;
        private bool ShouldSwapCrashAndRide => IsSplitMode && Player.Profile.SwapCrashAndRide;
        private bool LeftyFlip => Player.Profile.LeftyFlip;
        public override int FretCount => IsSplitMode ? 7 : 4;
        protected override int[] AllPads => Enum.GetValues(typeof(FourLaneDrumPad)).Cast<int>().Except(new[] { (int)FourLaneDrumPad.Kick }).ToArray();

        public FourDrumHighwayOrderingCalculator(YargPlayer player) : base(player) {}

#region GetPosition methods
        public override int GetPad(DrumsAction action)
        {
            return action switch
            {
                DrumsAction.RedDrum      => (int) FourLaneDrumPad.RedDrum,
                DrumsAction.YellowDrum   => (int) FourLaneDrumPad.YellowDrum,
                DrumsAction.BlueDrum     => (int) FourLaneDrumPad.BlueDrum,
                DrumsAction.GreenDrum    => (int) FourLaneDrumPad.GreenDrum,
                DrumsAction.YellowCymbal => (int) FourLaneDrumPad.YellowCymbal,
                DrumsAction.BlueCymbal   => (int) FourLaneDrumPad.BlueCymbal,
                DrumsAction.GreenCymbal  => (int) FourLaneDrumPad.GreenCymbal,
                _                        => -1,
            };
        }

        protected override int GetPosition(int pad)
        {
            var (leftCymbalFret, midCymbalFret, rightCymbalFret) = GetCymbalFrets();
            var (redDrum, yellowDrum, blueDrum, greenDrum) = GetDrumFrets();

            int position = (FourLaneDrumPad)pad switch
            {
                FourLaneDrumPad.RedDrum      => redDrum,
                FourLaneDrumPad.YellowDrum   => yellowDrum,
                FourLaneDrumPad.BlueDrum     => blueDrum,
                FourLaneDrumPad.GreenDrum    => greenDrum,
                FourLaneDrumPad.YellowCymbal => leftCymbalFret,
                FourLaneDrumPad.BlueCymbal   => midCymbalFret,
                FourLaneDrumPad.GreenCymbal  => rightCymbalFret,
                _ => -1,
            };

            return LeftyFlip ? FretCount - position - 1 : position;
        }

        private (int leftCymbalFret, int midCymbalFret, int rightCymbalFret) GetDefaultCymbalFretsForNonSplitMode()
        {
            // BlueRaja - This method will be used by the upcoming "Cymbal Lanes" feature
            return (1, 2, 3);
        }

        private (int leftCymbalFret, int midCymbalFret, int rightCymbalFret) GetDefaultCymbalFretsForSplitMode()
        {
            // BlueRaja - This method will be used by the upcoming "Cymbal Lanes" feature
            return (1, 3, 5);
        }

        private (int leftCymbalFret, int midCymbalFret, int rightCymbalFret) GetCymbalFrets()
        {
            var (leftCymbalFret, midCymbalFret, rightCymbalFret) = IsSplitMode
                ? GetDefaultCymbalFretsForSplitMode()
                : GetDefaultCymbalFretsForNonSplitMode();

            if (ShouldSwapSnareAndHiHat)
            {
                leftCymbalFret = 0; // BlueRaja - This logic will updated by the upcoming "Cymbal Lanes" feature
            }

            if (ShouldSwapCrashAndRide)
            {
                (midCymbalFret, rightCymbalFret) = (rightCymbalFret, midCymbalFret);
            }

            return (leftCymbalFret, midCymbalFret, rightCymbalFret);
        }

        private (int redDrum, int yellowDrum, int blueDrum, int greenDrum) GetDefaultDrumFretsForNonSplitMode()
        {
            return (0, 1, 2, 3);
        }

        private (int redDrum, int yellowDrum, int blueDrum, int greenDrum) GetDefaultDrumFretsForSplitMode()
        {
            // BlueRaja - This method will be used by the upcoming "Cymbal Lanes" feature
            return (0, 2, 4, 6);
        }

        private (int redDrum, int yellowDrum, int blueDrum, int greenDrum) GetDrumFrets()
        {
            var (redDrum, yellowDrum, blueDrum, greenDrum) = IsSplitMode
                ? GetDefaultDrumFretsForSplitMode()
                : GetDefaultDrumFretsForNonSplitMode();
            if (ShouldSwapSnareAndHiHat)
            {
                redDrum = 1; // BlueRaja - This logic will updated by the upcoming "Cymbal Lanes" feature
            }
            return (redDrum, yellowDrum, blueDrum, greenDrum);
        }
#endregion

#region Colors
        protected override int GetColorIndex(int pad)
        {
            var (leftCymbalColor, midCymbalColor, rightCymbalColor) = GetCymbalColors();
            var color = (FourLaneDrumPad)pad switch
            {
                FourLaneDrumPad.Kick         => FourLaneDrumsFret.Kick,
                FourLaneDrumPad.RedDrum      => FourLaneDrumsFret.RedDrum,
                FourLaneDrumPad.YellowDrum   => FourLaneDrumsFret.YellowDrum,
                FourLaneDrumPad.BlueDrum     => FourLaneDrumsFret.BlueDrum,
                FourLaneDrumPad.GreenDrum    => FourLaneDrumsFret.GreenDrum,
                FourLaneDrumPad.YellowCymbal => leftCymbalColor,
                FourLaneDrumPad.BlueCymbal   => midCymbalColor,
                FourLaneDrumPad.GreenCymbal  => rightCymbalColor,
                _                            => FourLaneDrumsFret.RedDrum,
            };
            return LeftyFlip ? (int)UpdateColorForLeftyFlip(color) : (int)color;
        }

        private (FourLaneDrumsFret leftCymbalColor, FourLaneDrumsFret midCymbalColor, FourLaneDrumsFret rightCymbalColor) GetCymbalColors()
        {
            // BlueRaja - This method will be used by the upcoming "Lanes to Show" feature
            return (FourLaneDrumsFret.YellowCymbal, FourLaneDrumsFret.BlueCymbal, FourLaneDrumsFret.GreenCymbal);
        }

        private FourLaneDrumsFret UpdateColorForLeftyFlip(FourLaneDrumsFret color)
        {
            // When LeftyMode is enabled, the Frets are switched visually, but not internally.
            // This leads to the colors being wrong, so we need to swap them around.
            return color switch
            {
                FourLaneDrumsFret.RedDrum => FourLaneDrumsFret.GreenDrum,
                FourLaneDrumsFret.YellowDrum => FourLaneDrumsFret.BlueDrum,
                FourLaneDrumsFret.BlueDrum => FourLaneDrumsFret.YellowDrum,
                FourLaneDrumsFret.GreenDrum => FourLaneDrumsFret.RedDrum,

                // We still associate each cymbal with the drum to its right, so the color associations
                // end up different for cymbals than drums
                FourLaneDrumsFret.RedCymbal => FourLaneDrumsFret.GreenCymbal,
                FourLaneDrumsFret.YellowCymbal => FourLaneDrumsFret.BlueCymbal,
                FourLaneDrumsFret.BlueCymbal => FourLaneDrumsFret.YellowCymbal,
                FourLaneDrumsFret.GreenCymbal => FourLaneDrumsFret.RedCymbal, 
                _ => color,
            };
        }
#endregion
    }
}