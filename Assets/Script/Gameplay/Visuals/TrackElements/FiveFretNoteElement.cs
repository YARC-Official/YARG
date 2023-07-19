using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Settings.ColorProfiles;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveFretNoteElement : NoteElement<GuitarNote, FiveFretPlayer>
    {
        [SerializeField]
        private NoteGroup _strumGroup;
        [SerializeField]
        private NoteGroup _hopoGroup;
        [SerializeField]
        private NoteGroup _tapGroup;

        [Space]
        [SerializeField]
        private SustainLine _sustainLine;

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset =>
            (float) NoteRef.TimeLength * Player.Player.Profile.NoteSpeed;

        private bool _notesHiddenForSustain = false;

        protected override void InitializeElement()
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
                _                    => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
            };

            // Show and set material properties
            NoteGroup.SetActive(true);
            // TODO: Note material seed

            // Set line length
            if (NoteRef.IsSustain)
            {
                _notesHiddenForSustain = false;
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.Player.Profile.NoteSpeed;
                _sustainLine.SetInitialLength(len);
            }

            // Set note and sustain color
            UpdateColor();
        }

        protected override void UpdateElement()
        {
            if (NoteRef.WasHit)
            {
                if (NoteRef.IsSustain)
                {
                    // Once hit, hide the notes but still show sustain
                    if (!_notesHiddenForSustain)
                    {
                        HideNotes();
                        _notesHiddenForSustain = true;
                    }

                    _sustainLine.UpdateLengthForHit();
                }
                else
                {
                    ParentPool.Return(this);
                    return;
                }
            }

            UpdateColor();
        }

        private void UpdateColor()
        {
            // Get which note color to use
            var color = NoteRef.IsStarPower
                ? ColorProfile.Default.FiveFret.StarpowerNoteColor
                : ColorProfile.Default.FiveFret.NoteColors[NoteRef.Fret];

            // Set the note color
            NoteGroup.ColoredMaterial.color = color;

            // Set the sustain color
            if (!NoteRef.WasMissed)
            {
                _sustainLine.SetColor(color);
            }
            else
            {
                _sustainLine.SetMissed();
            }
        }

        private void HideNotes()
        {
            _strumGroup.SetActive(false);
            _hopoGroup.SetActive(false);
            _tapGroup.SetActive(false);
        }

        protected override void HideElement()
        {
            HideNotes();

            _sustainLine.gameObject.SetActive(false);
        }
    }
}