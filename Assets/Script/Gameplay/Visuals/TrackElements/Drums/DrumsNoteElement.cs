using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public abstract class DrumsNoteElement : NoteElement<DrumNote, DrumsPlayer>, IThemeNoteCreator
    {
        private const float SPLIT_LANE_SCALE_FACTOR = 4f / 7f;

        private Vector3 normalScale;
        private Vector3 splitScale;

        protected enum NoteType
        {
            Normal        = 0,
            Cymbal        = 1,
            Kick          = 2,
            Accent        = 3,
            Ghost         = 4,
            CymbalAccent  = 5,
            CymbalGhost   = 6,

            Count
        }

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starpowerModels)
        {
            CreateNoteGroupArrays((int) NoteType.Count);

            AssignNoteGroup(models, starpowerModels, (int) NoteType.Normal,         ThemeNoteType.Normal);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Cymbal,         ThemeNoteType.Cymbal);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Kick,           ThemeNoteType.Kick);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Accent,         ThemeNoteType.Accent);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Ghost,          ThemeNoteType.Ghost);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.CymbalAccent,   ThemeNoteType.CymbalAccent);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.CymbalGhost,    ThemeNoteType.CymbalGhost);
        }

        public override void HitNote()
        {
            base.HitNote();

            ParentPool.Return(this);
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        protected abstract void UpdateColor();

        protected override void HideElement()
        {
            HideNotes();
        }

        protected int GetNoteGroup(bool isCymbal)
        {
            if (NoteRef.IsAccent)
            {
                return (int) (isCymbal ? NoteType.CymbalAccent : NoteType.Accent);
            }

            if (NoteRef.IsGhost)
            {
                return (int) (isCymbal ? NoteType.CymbalGhost : NoteType.Ghost);
            }

            return (int) (isCymbal ? NoteType.Cymbal : NoteType.Normal);
        }

        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            normalScale = transform.localScale;
            var newScale = transform.localScale;
            newScale.Scale(new(SPLIT_LANE_SCALE_FACTOR, 1f, SPLIT_LANE_SCALE_FACTOR));
            splitScale = newScale;
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            if (Player.Player.Profile.CurrentInstrument == Core.Instrument.ProDrums && Player.Player.Profile.SplitProTomsAndCymbals)
            {
                gameObject.transform.localScale = NoteRef.Pad switch
                {
                    0 => normalScale,
                    _ => splitScale
                };
            }
        }
    }
}