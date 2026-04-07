using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveFretGuitarNoteElement : NoteElement<GuitarNote, FiveFretGuitarPlayer>
    {
        private enum NoteType
        {
            Strum    = 0,
            HOPO     = 1,
            Tap      = 2,
            Open     = 3,
            OpenHOPO = 4,

            Count
        }

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;

        private SustainLine _sustainLine;

        // Only for five fret, so it's defined here
        public int[] OpenChordFrets { get; set; } = Array.Empty<int>();

        private Matrix4x4 _chordMatrix = new();

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        private const float CHORD_DIM_MARGIN = 0.02f;
        private const float ONE_FRET_WIDTH   = 0.2f;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays((int) NoteType.Count);

            AssignNoteGroup(models, starPowerModels, (int) NoteType.Strum,    ThemeNoteType.Normal);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.HOPO,     ThemeNoteType.HOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Tap,      ThemeNoteType.Tap);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Open,     ThemeNoteType.Open);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.OpenHOPO, ThemeNoteType.OpenHOPO);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Fret != (int) FiveFretGuitarFret.Open)
            {
                // Deal with non-open notes
                var lane = Player.GetLanePosition((FiveFretGuitarFret)NoteRef.Fret);

                // Set the position
                transform.localPosition = new Vector3(GetElementX(lane, FiveFretGuitarPlayer.LANE_COUNT), 0f, 0f);

                // Get which note model to use
                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Strum],
                    GuitarNoteType.Hopo  => noteGroups[(int) NoteType.HOPO],
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.Tap],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
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
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Open],
                    GuitarNoteType.Hopo or
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.OpenHOPO],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _openSustainLine;
            }

            if (NoteRef.Fret == (int) FiveFretGuitarFret.Open)
            {
                NoteGroup.NotePositions = SetOpenChordInfo(OpenChordFrets);
            }

            // Show and set material properties
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

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

        private Matrix4x4 SetOpenChordInfo(int[] frets)
        {
            // Take frets and pack them into the matrix representing uv values that need to be dimmed

            _chordMatrix = new Matrix4x4();

            float lower;
            float upper;
            int row = 0;

            for (int i = 0; i < frets.Length; i++)
            {
                // This only combines adjacent frets, but that's all we need since we just need to get it down
                // to no more than 3 dimmed regions for the shader
                if (i + 1 < frets.Length && frets[i] == frets[i + 1] - 1)
                {
                    // Use lower from current fret and upper from next fret
                    lower = (frets[i] - 1) * ONE_FRET_WIDTH;
                    // two because we're coalescing two frets here
                    upper = lower + ONE_FRET_WIDTH * 2;

                    _chordMatrix[i, 0] = lower;
                    _chordMatrix[i, 1] = upper;

                    // Skip the next fret since we already accounted for it
                    i++;
                }
                else
                {
                    lower = (frets[i] - 1) * ONE_FRET_WIDTH;
                    upper = lower + ONE_FRET_WIDTH;
                }

                // These are being inverted, so instead of 0 = lower, 1 = upper it's 0 = 1 - upper, 1 = 1 - lower
                _chordMatrix[row, 0] = (1 - upper) - CHORD_DIM_MARGIN;
                _chordMatrix[row, 1] = (1 - lower) + CHORD_DIM_MARGIN;

                row++;
            }

            return _chordMatrix;
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


        public override void MissNote()
        {
            base.MissNote();

            UpdateColor();
        }

        protected override void UpdateElement()
        {
            base.UpdateElement();

            UpdateSustain();
        }

        protected override void OnNoteStateChanged()
        {
            base.OnNoteStateChanged();

            UpdateColor();
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        protected override bool CalcStarPowerVisible()
        {
            if (!NoteRef.IsStarPower)
            {
                return false;
            }
            return !(((GuitarEngineParameters) Player.BaseParameters).NoStarPowerOverlap && Player.BaseStats.IsStarPowerActive);
        }

        private void UpdateSustain()
        {
            _sustainLine.UpdateSustainLine();
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FiveFretGuitar;

            // Get which note color to use
            var colorNoStarPower = colors.GetNoteColor(NoteRef.Fret);
            var color = IsStarPowerVisible
                ? colors.GetNoteStarPowerColor(NoteRef.Fret)
                : colorNoStarPower;

            if (NoteRef.WasMissed)
            {
                color = colors.Miss;
            }

            // Set the note color if not hidden
            if (!NoteRef.WasHit)
            {
                NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());

                // Set the metal color
                NoteGroup.SetMetalColor(colors.GetMetalColor(IsStarPowerVisible).ToUnityColor());
            }

            // The rest of this method is for sustain only
            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, color.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
        }

        public override void DisableIntoPool()
        {
            OpenChordFrets = Array.Empty<int>();
            base.DisableIntoPool();
        }
    }
}