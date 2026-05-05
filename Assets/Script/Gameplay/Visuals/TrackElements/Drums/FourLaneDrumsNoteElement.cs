using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using static YARG.Core.Game.ColorProfile;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public sealed class FourLaneDrumsNoteElement : DrumsNoteElement
    {
        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Pad != 0 && NoteRef.Pad != (int) FourLaneDrumPad.Wildcard)
            {
                // Deal with non-kick/wildcard notes
                var position = Player.GetHighwayOrderingInfo(NoteRef.Pad).Position;

                bool isCymbal = NoteRef.Pad >= (int) FourLaneDrumPad.YellowCymbal;

                // Set the position
                transform.localPosition = new Vector3(GetElementX(position, Player.LaneCount), 0f, 0f);

                // Get which note model to use
                NoteGroup = noteGroups[GetNoteGroup(isCymbal)];
            }
            else if (NoteRef.Pad == 0 && Player.NumberOfDedicatedKickLanes > 0)
            {
                // Deal with dedicated-lane kick notes
                int highwayIndex;
                if (NoteRef.IsDoubleKick && Player.NumberOfDedicatedKickLanes == 2)
                {
                    highwayIndex = DrumsPlayer.DOUBLE_KICK_FRET_INDEX;
                }
                else
                {
                    highwayIndex = (int)FourLaneDrumPad.Kick;
                }

                // Set the position
                var position = Player.GetHighwayOrderingInfo(highwayIndex).Position;
                transform.localPosition = new Vector3(GetElementX(position, Player.LaneCount), 0f, 0f);

                NoteGroup = noteGroups[(int) NoteType.DedicatedLaneKick];
            }
            else
            {
                // Deal with wildcard and regular kick notes
                var groupIndex = NoteRef.Pad == 0 ? (int)NoteType.Kick : (int)NoteType.Wildcard;
                transform.localPosition = Vector3.zero;
                NoteGroup = noteGroups[groupIndex];
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
            var colors = Player.Player.ColorProfile.FourLaneDrums;

            // Get pad index
            int colorIndex;

            if (NoteRef.IsDoubleKick && Player.NumberOfDedicatedKickLanes is 2)
            {
                colorIndex = (int)FourLaneDrumsFret.DoubleKick;
            }
            else
            {
                colorIndex = Player.GetHighwayOrderingInfo(NoteRef.Pad).ColorIndex;
            }

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