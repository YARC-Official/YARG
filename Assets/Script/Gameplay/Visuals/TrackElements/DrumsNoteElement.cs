using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public abstract class DrumsNoteElement : NoteElement<DrumNote, DrumsPlayer>, IThemePrefabCreator
    {
        [SerializeField]
        protected NoteGroup NormalGroup;
        [SerializeField]
        protected NoteGroup CymbalGroup;
        [SerializeField]
        protected NoteGroup KickGroup;

        public override void HitNote()
        {
            base.HitNote();

            ParentPool.Return(this);
        }

        protected override void UpdateElement()
        {
            // Color should be updated every frame in case of starpower state changes
            UpdateColor();
        }

        protected abstract void UpdateColor();

        protected override void HideElement()
        {
            NormalGroup.SetActive(false);
            CymbalGroup.SetActive(false);
            KickGroup.SetActive(false);
        }

        public void SetModels(Dictionary<ThemeNoteType, GameObject> models)
        {
            NormalGroup.SetModelFromTheme(models[ThemeNoteType.Normal]);
            CymbalGroup.SetModelFromTheme(models[ThemeNoteType.Cymbal]);
            KickGroup.SetModelFromTheme(models[ThemeNoteType.Kick]);
        }
    }
}