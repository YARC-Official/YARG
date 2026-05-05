using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public abstract class DrumsNoteElement : NoteElement<DrumNote, DrumsPlayer>, IThemeNoteCreator
    {
        
        private Vector3? _scalingFactor = null;

        protected enum NoteType
        {
            Normal        = 0,
            Cymbal        = 1,
            Kick          = 2,
            Accent        = 3,
            Ghost         = 4,
            CymbalAccent  = 5,
            CymbalGhost   = 6,
            Wildcard      = 7,
            DedicatedLaneKick = 8,

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
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Wildcard,       ThemeNoteType.Wildcard);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.DedicatedLaneKick, ThemeNoteType.DedicatedLaneKick);
        }

        public override void HitNote()
        {
            base.HitNote();

            ParentPool.Return(this);
        }

        protected override bool CalcStarPowerVisible()
        {
            if (!NoteRef.IsStarPower)
            {
                return false;
            }
            return !(((DrumsEngineParameters) Player.BaseParameters).NoStarPowerOverlap && Player.BaseStats.IsStarPowerActive);
        }

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

        protected override void InitializeElement()
        {
            base.InitializeElement();

            _scalingFactor ??= new Vector3(Player.NoteScaleFactor, Player.NoteScaleFactor, Player.NoteScaleFactor);

            if ((NoteRef.Pad != 0 || Player.NumberOfDedicatedKickLanes > 0) && NoteRef.Pad != (int)FourLaneDrumPad.Wildcard)
            {
                gameObject.transform.localScale = Vector3.Scale(transform.localScale, _scalingFactor.Value);
            }
        }

        protected abstract void UpdateColor();

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        protected static int GetColorFromPulse(int color, float pulse)
        {
            float intensity = Mathf.Pow(pulse - 1, 3) + 1f;
            return (int) (intensity * color);
        }
    }
}