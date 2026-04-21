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
            Strum    = 0,
            HOPO     = 1,
            Tap      = 2,
            Open     = 3,
            OpenHOPO = 4,
            Wildcard = 5,

            Count
        }

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;
        [SerializeField]
        private SustainLine _wildcardSustainLine;

        private SustainLine _sustainLine;

        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

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
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Wildcard, ThemeNoteType.Wildcard);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Fret != (int) SixFretGuitarFret.Open && NoteRef.Fret != (int) SixFretGuitarFret.Wildcard)
            {
                var lane = Player.GetLanePosition((SixFretGuitarFret)NoteRef.Fret);

                transform.localPosition = new Vector3(GetElementX(lane, Player.LaneCount), 0f, 0f);

                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Strum],
                    GuitarNoteType.Hopo  => noteGroups[(int) NoteType.HOPO],
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.Tap],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _normalSustainLine;
            }
            else if (NoteRef.Fret == (int) SixFretGuitarFret.Open)
            {
                transform.localPosition = Vector3.zero;

                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Open],
                    GuitarNoteType.Hopo or
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.OpenHOPO],
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

      protected void UpdateColor()
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

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
            _wildcardSustainLine.gameObject.SetActive(false);
        }
    }
}
