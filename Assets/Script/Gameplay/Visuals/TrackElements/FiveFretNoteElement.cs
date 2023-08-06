using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveFretNoteElement : NoteElement<GuitarNote, FiveFretPlayer>
    {
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private NoteGroup _strumGroup;
        [SerializeField]
        private NoteGroup _hopoGroup;
        [SerializeField]
        private NoteGroup _tapGroup;
        [SerializeField]
        private NoteGroup _openGroup;
        [SerializeField]
        private NoteGroup _openHopoGroup;

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;

        private SustainLine _sustainLine;

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset =>
            (float) NoteRef.TimeLength * Player.NoteSpeed;

        protected override void InitializeElement()
        {
            base.InitializeElement();

            if (NoteRef.Fret != 0)
            {
                // Deal with non-open notes

                // Set the position
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

                _sustainLine = _normalSustainLine;
            }
            else
            {
                // Deal with open notes

                // Set the position
                transform.localPosition = Vector3.zero;

                // Get which note model to use
                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => _openGroup,
                    GuitarNoteType.Hopo  => _openHopoGroup,
                    _                    => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _openSustainLine;
            }

            // Show and set material properties
            NoteGroup.SetActive(true);
            NoteGroup.InitializeRandomness();

            // Set line length
            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);
            }

            // Set note and sustain color
            UpdateColor();
        }

        public override void HitNote()
        {
            base.HitNote();

            if (NoteRef.IsSustain)
            {
                HideNotes();
            }
            else
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
            // Color should be updated every frame in case of starpower state changes
            UpdateColor();

            // Update sustain line
            UpdateSustain();
        }

        private void UpdateSustain()
        {
            if (!NoteRef.WasHit) return;

            _sustainLine.UpdateSustainLine(Player.NoteSpeed);
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FiveFretGuitar;

            // Get which note color to use
            var color = NoteRef.IsStarPower
                ? colors.GetNoteStarPowerColor(NoteRef.Fret)
                : colors.GetNoteColor(NoteRef.Fret);

            // Set the note color
            NoteGroup.ColoredMaterial.color = color.ToUnityColor();
            NoteGroup.ColoredMaterial.SetColor(_emissionColor, color.ToUnityColor() * 8f);

            // TODO: Temporary
            // Change color for open HOPOs
            if (NoteRef.Fret == 0 && NoteRef.Type == GuitarNoteType.Hopo)
            {
                NoteGroup.ColoredMaterial.color += new Color(3f, 3f, 3f, 0f);
            }

            // The rest of this method is for sustain only
            if (!NoteRef.IsSustain) return;

            _sustainLine.SetColor(SustainState, color.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
        }

        private void HideNotes()
        {
            _strumGroup.SetActive(false);
            _hopoGroup.SetActive(false);
            _tapGroup.SetActive(false);
            _openGroup.SetActive(false);
            _openHopoGroup.SetActive(false);
        }
    }
}