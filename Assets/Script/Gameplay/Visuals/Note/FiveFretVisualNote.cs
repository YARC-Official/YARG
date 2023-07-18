using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Settings.ColorProfiles;

namespace YARG.Gameplay.Visuals
{
    public class FiveFretVisualNote : VisualNote<GuitarNote, FiveFretPlayer>
    {
        [SerializeField]
        private NoteGroup _strumGroup;

        [SerializeField]
        private NoteGroup _hopoGroup;

        [SerializeField]
        private NoteGroup _tapGroup;

        protected override void InitializeNote()
        {
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            // Get which note model to use
            NoteGroup = NoteRef.Type switch
            {
                GuitarNoteType.Strum => _strumGroup,
                GuitarNoteType.Hopo  => _hopoGroup,
                GuitarNoteType.Tap   => _tapGroup,
                _                    => throw new Exception("Unreachable.")
            };

            // Get which note color to use
            var color = NoteRef.IsStarPower
                ? ColorProfile.Default.FiveFret.StarpowerNoteColor
                : ColorProfile.Default.FiveFret.NoteColors[NoteRef.Fret];

            // Show and set material!
            NoteGroup.SetActive(true);
            NoteGroup.ColoredMaterial.color = color;
        }

        protected override void HideNote()
        {
            _strumGroup.SetActive(false);
            _hopoGroup.SetActive(false);
            _tapGroup.SetActive(false);
        }

        protected override void Update()
        {
            if (NoteRef.IsStarPower)
            {
                NoteGroup.ColoredMaterial.color = ColorProfile.Default.FiveFret.StarpowerNoteColor;
            }
            else
            {
                NoteGroup.ColoredMaterial.color = ColorProfile.Default.FiveFret.NoteColors[NoteRef.Fret];
            }
            base.Update();
        }
    }
}