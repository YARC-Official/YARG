using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class SixFretGuitarNoteElement : NoteElement<GuitarNote, SixFretGuitarPlayer>
    {
        private enum NoteType
        {
            Strum = 0,
            HOPO = 1,
            Tap = 2,
            Open = 3,
            OpenHOPO = 4,
            Wildcard = 5,

            Count
        }

        // If out of a black/white pair only one note show and split option is not set
        // it has it's width increased by this to cover both black and white lanes
        private const float SINGLE_NOTE_MULTIPLIER = 1.95f;
        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;
        [SerializeField]
        private SustainLine _wildcardSustainLine;

        private SustainLine _sustainLine;

        /// <summary>
        /// Whether this note has a paired sibling in the same combined lane.
        /// Only meaningful when SixFretSplitLanes is false.
        /// </summary>
        public bool IsPaired = false;

        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays((int) NoteType.Count);

            AssignNoteGroup(models, starPowerModels, (int) NoteType.Strum, ThemeNoteType.Normal);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.HOPO, ThemeNoteType.HOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Tap, ThemeNoteType.Tap);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Open, ThemeNoteType.Open);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.OpenHOPO, ThemeNoteType.OpenHOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Wildcard, ThemeNoteType.Wildcard);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;

            int lane = -1;

            if (NoteRef.Fret != (int) SixFretGuitarFret.Open && NoteRef.Fret != (int) SixFretGuitarFret.Wildcard)
            {
                lane = Player.GetLanePosition((SixFretGuitarFret) NoteRef.Fret);

                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Strum],
                    GuitarNoteType.Hopo => noteGroups[(int) NoteType.HOPO],
                    GuitarNoteType.Tap => noteGroups[(int) NoteType.Tap],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _normalSustainLine;

                if (!Player.Player.Profile.SixFretSplitLanes && !IsPaired)
                {
                    // Combined mode, solo note: center between pair, scale wider
                    float combinedX = GetCombinedCenterX(lane);
                    transform.localPosition = new Vector3(combinedX, 0f, 0f);

                    // Scale note group to span both lanes
                    var s = NoteGroup.transform.localScale;
                    NoteGroup.transform.localScale = new Vector3(s.x * SINGLE_NOTE_MULTIPLIER, s.y, s.z);
                }
                else
                {
                    // Split mode or paired: normal
                    transform.localPosition = new Vector3(GetElementX(lane, Player.LaneCount), 0f, 0f);
                }
            }
            else if (NoteRef.Fret == (int) SixFretGuitarFret.Open)
            {
                transform.localPosition = Vector3.zero;

                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Open],
                    GuitarNoteType.Hopo or
                    GuitarNoteType.Tap => noteGroups[(int) NoteType.OpenHOPO],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _openSustainLine;
            }
            else
            {
                transform.localPosition = Vector3.zero;

                NoteGroup = noteGroups[(int) NoteType.Wildcard];

                _sustainLine = _wildcardSustainLine;
            }

            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);
            }

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

        private float GetCombinedCenterX(int fretLane)
        {
            int pairLane = GetPairedLane(fretLane);
            float x1 = GetElementX(fretLane, Player.LaneCount);
            float x2 = GetElementX(pairLane, Player.LaneCount);
            return (x1 + x2) / 2f;
        }

        private int GetPairedLane(int fretLane)
        {
            // Highway ordering: Black1=0, White1=1, Black2=2, White2=3, Black3=4, White3=5
            // Pairs: (0,1), (2,3), (4,5) — always adjacent even/odd
            return fretLane % 2 == 0 ? fretLane + 1 : fretLane - 1;
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.SixFretGuitar;

            var colorNoStarPower = colors.GetNoteColor(NoteRef.Fret);
            var color = IsStarPowerVisible
                ? colors.GetNoteStarPowerColor(NoteRef.Fret)
                : colorNoStarPower;

            if (NoteRef.WasMissed)
            {
                color = colors.Miss;
            }

            if (!NoteRef.WasHit)
            {
                NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());

                NoteGroup.SetMetalColor(colors.GetMetalColor(IsStarPowerVisible).ToUnityColor());
            }

            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, color.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();

            // Reset note group X scales (combined mode doubles them)
            foreach (var group in NoteGroups)
            {
                if (group != null)
                {
                    var s = group.transform.localScale;
                    group.transform.localScale = new Vector3(1f, s.y, s.z);
                }
            }
            foreach (var group in StarPowerNoteGroups)
            {
                if (group != null)
                {
                    var s = group.transform.localScale;
                    group.transform.localScale = new Vector3(1f, s.y, s.z);
                }
            }

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
            _wildcardSustainLine.gameObject.SetActive(false);
        }
    }
}
