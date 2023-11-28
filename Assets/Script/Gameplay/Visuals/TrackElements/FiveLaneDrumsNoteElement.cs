using UnityEngine;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveLaneDrumsNoteElement : DrumsNoteElement
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

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
                if (SettingsManager.Settings.UseCymbalModelsInFiveLane.Data)
                {
                    NoteGroup = (FiveLaneDrumPad) NoteRef.Pad switch
                    {
                        FiveLaneDrumPad.Yellow => noteGroups[(int) NoteType.Cymbal],
                        FiveLaneDrumPad.Orange => noteGroups[(int) NoteType.Cymbal],
                        _                      => noteGroups[(int) NoteType.Normal]
                    };
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