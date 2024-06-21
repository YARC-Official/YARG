using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public sealed class FourLaneDrumsNoteElement : DrumsNoteElement
    {
        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Pad != 0)
            {
                // Deal with non-kick notes

                // Shift cymbals into their correct lanes
                int lane = NoteRef.Pad;
                bool isCymbal = lane >= (int) FourLaneDrumPad.YellowCymbal;
                if (isCymbal)
                {
                    lane -= 3;
                }

                // Set the position
                transform.localPosition = new Vector3(GetElementX(lane, 4), 0f, 0f) * LeftyFlipMultiplier;

                // Get which note model to use
                NoteGroup = noteGroups[GetNoteGroup(isCymbal)];
            }
            else
            {
                // Deal with kick notes
                transform.localPosition = Vector3.zero;
                NoteGroup = noteGroups[(int) NoteType.Kick];
            }

            // Show and set material properties
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            // Set note color
            UpdateColor();
        }

        protected override void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FourLaneDrums;

            // Get pad index
            int pad = NoteRef.Pad;
            if (LeftyFlip)
            {
                pad = (FourLaneDrumPad) pad switch
                {
                    FourLaneDrumPad.Kick         => (int) FourLaneDrumPad.Kick,
                    FourLaneDrumPad.RedDrum      => (int) FourLaneDrumPad.GreenDrum,
                    FourLaneDrumPad.YellowDrum   => (int) FourLaneDrumPad.BlueDrum,
                    FourLaneDrumPad.BlueDrum     => (int) FourLaneDrumPad.YellowDrum,
                    FourLaneDrumPad.GreenDrum    => (int) FourLaneDrumPad.RedDrum,
                    FourLaneDrumPad.YellowCymbal => (int) FourLaneDrumPad.BlueCymbal,
                    FourLaneDrumPad.BlueCymbal   => (int) FourLaneDrumPad.YellowCymbal,
                    FourLaneDrumPad.GreenCymbal  => 8, // The forbidden red cymbal
                    _                            => throw new Exception("Unreachable.")
                };
            }

            // Get colors
            var colorNoStarPower = colors.GetNoteColor(pad);
            var color = colorNoStarPower;
            if (NoteRef.IsStarPowerActivator && Player.Engine.CanStarPowerActivate)
            {
                color = colors.ActivationNote;
            }
            else if (NoteRef.IsStarPower)
            {
                color = colors.GetNoteStarPowerColor(pad);
            }

            // Set the note color
            NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());
        }
    }
}