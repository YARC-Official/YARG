using UnityEngine;
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
                NoteGroup = isCymbal ? CymbalGroup : NormalGroup;
            }
            else
            {
                // Deal with kick notes
                transform.localPosition = Vector3.zero;
                NoteGroup = KickGroup;
            }

            // Show and set material properties
            NoteGroup.SetActive(true);
            NoteGroup.InitializeRandomness();

            // Set note color
            UpdateColor();
        }

        protected override void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FourLaneDrums;

            // Get which note color to use
            Color color;
            if (NoteRef.IsStarPowerActivator && Player.Engine.EngineStats.StarPowerAmount >= 0.5)
            {
                color = colors.ActivationNote.ToUnityColor();
            }
            else
            {
                color = (NoteRef.IsStarPower
                    ? colors.GetNoteStarPowerColor(NoteRef.Pad)
                    : colors.GetNoteColor(NoteRef.Pad))
                    .ToUnityColor();
            }

            // Set the note color
            NoteGroup.ColoredMaterial.color = color;

            // Set emission
            float emissionMultiplier = NoteRef.Pad == (int) FourLaneDrumPad.Kick ? 8f : 2.5f;
            NoteGroup.ColoredMaterial.SetColor(_emissionColor, color * emissionMultiplier);
        }
    }
}