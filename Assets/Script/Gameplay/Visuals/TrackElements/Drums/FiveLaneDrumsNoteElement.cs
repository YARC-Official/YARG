using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Settings;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveLaneDrumsNoteElement : DrumsNoteElement
    {
        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Pad != 0)
            {
                // Deal with non-kick notes
                var position = Player.GetHighwayOrderingInfo(NoteRef.Pad).Position;
                
                // Set the position
                transform.localPosition = new Vector3(GetElementX(position, Player.LaneCount), 0f, 0f);

                // Get which note model to use
                if (Player.Player.Profile.UseCymbalModels)
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

        protected override void UpdateElement()
        {
            // Potentially update flash in case of activation note
            UpdateColor();
        }

        protected override void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FiveLaneDrums;

            // Get pad index
            var colorIndex = Player.GetHighwayOrderingInfo(NoteRef.Pad).ColorIndex;
            
            // Get colors
            var colorNoStarPower = colors.GetNoteColor(colorIndex);
            var color = colorNoStarPower;

            if (NoteRef.WasMissed)
            {
                color = colors.Miss;
            }
            else if (NoteRef.IsStarPowerActivator && Player.Engine.CanStarPowerActivate && !Player.Engine.BaseStats.IsStarPowerActive)
            {
                float pulse = (float) GameManager.BeatEventHandler.Visual.StrongBeat.CurrentPercentage;
                var fullColor = colors.GetActivationNoteColor(colorIndex);
                color = Color.FromArgb(
                    fullColor.A,
                    GetColorFromPulse(fullColor.R, pulse),
                    GetColorFromPulse(fullColor.G, pulse),
                    GetColorFromPulse(fullColor.B, pulse)
                );
            }
            else if (IsStarPowerVisible)
            {
                color = colors.GetNoteStarPowerColor(colorIndex);
            }

            // Set the note color if not hidden
            if (!NoteRef.WasHit)
            {
                NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());

                // Set the metal color
                NoteGroup.SetMetalColor(colors.GetMetalColor(IsStarPowerVisible).ToUnityColor());
            }
        }
    }
}