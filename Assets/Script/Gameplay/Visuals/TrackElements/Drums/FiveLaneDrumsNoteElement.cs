using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveLaneDrumsNoteElement : DrumsNoteElement
    {
        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Pad != 0)
            {
                // Deal with non-kick notes

                // Set the position
                transform.localPosition = new Vector3(GetElementX(NoteRef.Pad, 5), 0f, 0f) * LeftyFlipMultiplier;

                // Get which note model to use
                if (SettingsManager.Settings.UseCymbalModelsInFiveLane.Value)
                {
                    bool isCymbal = (FiveLaneDrumPad) NoteRef.Pad is FiveLaneDrumPad.Yellow or FiveLaneDrumPad.Orange;

                    NoteGroup = noteGroups[GetNoteGroup(isCymbal)];
                }
                else
                {
                    NoteGroup = noteGroups[(int) NoteType.Normal];
                }
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
            var colors = Player.Player.ColorProfile.FiveLaneDrums;

            // Get pad index
            int pad = NoteRef.Pad;
            if (LeftyFlip)
            {
                pad = (FiveLaneDrumPad) pad switch
                {
                    FiveLaneDrumPad.Kick   => (int) FiveLaneDrumPad.Kick,
                    FiveLaneDrumPad.Red    => (int) FiveLaneDrumPad.Green,
                    FiveLaneDrumPad.Yellow => (int) FiveLaneDrumPad.Orange,
                    FiveLaneDrumPad.Blue   => (int) FiveLaneDrumPad.Blue,
                    FiveLaneDrumPad.Orange => (int) FiveLaneDrumPad.Yellow,
                    FiveLaneDrumPad.Green  => (int) FiveLaneDrumPad.Red,
                    _                      => throw new Exception("Unreachable.")
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