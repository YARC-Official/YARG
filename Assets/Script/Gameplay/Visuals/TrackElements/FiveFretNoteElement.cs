﻿using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveFretNoteElement : NoteElement<GuitarNote, FiveFretPlayer>, IThemePrefabCreator
    {
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
        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        protected override void InitializeElement()
        {
            base.InitializeElement();

            if (NoteRef.Fret != 0)
            {
                // Deal with non-open notes

                // Set the position
                transform.localPosition = new Vector3(GetElementX(NoteRef.Fret, 5), 0f, 0f) * LeftyFlipMultiplier;

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
                    GuitarNoteType.Hopo or GuitarNoteType.Tap => _openHopoGroup,
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
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
            NoteGroup.SetColorWithEmission(color.ToUnityColor());

            // TODO: Temporary
            // Change color for open HOPOs/Taps
            if (NoteRef.Fret == 0 && NoteRef.Type is GuitarNoteType.Hopo or GuitarNoteType.Tap)
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

        public void SetModels(Dictionary<ThemeNoteType, GameObject> models)
        {
            _strumGroup.SetModelFromTheme(models[ThemeNoteType.Normal]);
            _hopoGroup.SetModelFromTheme(models[ThemeNoteType.HOPO]);
            _tapGroup.SetModelFromTheme(models[ThemeNoteType.Tap]);
            _openGroup.SetModelFromTheme(models[ThemeNoteType.Open]);
            _openHopoGroup.SetModelFromTheme(models[ThemeNoteType.OpenHOPO]);
        }
    }
}