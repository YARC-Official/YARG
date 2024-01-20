using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public abstract class DrumsNoteElement : NoteElement<DrumNote, DrumsPlayer>, IThemePrefabCreator
    {
        protected enum NoteType
        {
            Normal = 0,
            Cymbal = 1,
            Kick   = 2,

            Count
        }

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starpowerModels)
        {
            CreateNoteGroupArrays((int) NoteType.Count);

            AssignNoteGroup(models, starpowerModels, (int) NoteType.Normal, ThemeNoteType.Normal);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Cymbal, ThemeNoteType.Cymbal);
            AssignNoteGroup(models, starpowerModels, (int) NoteType.Kick,   ThemeNoteType.Kick);
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
    }
}