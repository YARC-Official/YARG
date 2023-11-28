﻿using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public sealed class FourLaneDrumsNoteElement : DrumsNoteElement
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

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
                NoteGroup = isCymbal ? noteGroups[(int) NoteType.Cymbal] : noteGroups[(int) NoteType.Normal];
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

            // Get colors
            var colorNoStarPower = colors.GetNoteColor(NoteRef.Pad);
            var color = colorNoStarPower;
            if (NoteRef.IsStarPowerActivator && Player.Engine.EngineStats.StarPowerAmount >= 0.5)
            {
                color = colors.ActivationNote;
            }
            else if (NoteRef.IsStarPower)
            {
                color = colors.GetNoteStarPowerColor(NoteRef.Pad);
            }

            // Set the note color
            float emissionMultiplier = NoteRef.Pad == (int) FiveLaneDrumPad.Kick ? 8f : 2.5f;
            NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor(),
                emissionMultiplier);
        }
    }
}