using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    using ThemeDict = Dictionary<ThemeNoteType, GameObject>;

    public enum SustainState
    {
        Waiting,
        Hitting,
        Missed
    }

    public abstract class NoteElement<TNote, TPlayer> : TrackElement<TPlayer>, IThemePrefabCreator
        where TNote : Note<TNote>
        where TPlayer : TrackPlayer
    {
        public TNote NoteRef { get; set; }

        protected SustainState SustainState { get; private set; }

        // Using all both at these attributes at once may seem weird, but only
        // SerializeFields are passed along properly when being duplicated.
        // At the same time, we don't want these to be displayed in the inspector.
        [HideInInspector]
        [SerializeField]
        protected NoteGroup[] NoteGroups;
        [HideInInspector]
        [SerializeField]
        protected NoteGroup[] StarPowerNoteGroups;

        protected NoteGroup NoteGroup;

        public override double ElementTime => NoteRef.Time;

        private bool _lastStarPowerState;

        public abstract void SetThemeModels(ThemeDict models, ThemeDict starPowerModels);

        protected override void InitializeElement()
        {
            SustainState = SustainState.Waiting;
            _lastStarPowerState = NoteRef.IsStarPower;
        }

        protected override void UpdateElement()
        {
            if (_lastStarPowerState != NoteRef.IsStarPower)
            {
                OnStarPowerStateChanged();
                _lastStarPowerState = NoteRef.IsStarPower;
            }
        }

        protected virtual void OnStarPowerStateChanged()
        {
            // If we still have star power, skip
            if (NoteRef.IsStarPower) return;

            // If we did have star power, and the user lost it, then swap the model out
            int index = Array.IndexOf(StarPowerNoteGroups, NoteGroup);
            if (index != -1 && NoteGroup != NoteGroups[index])
            {
                // Disable the old note group
                NoteGroup.SetActive(false);

                // Enable the new one
                NoteGroup = NoteGroups[index];
                NoteGroup.SetActive(true);
                NoteGroup.InitializeRandomness();
            }
        }

        public virtual void HitNote()
        {
            SustainState = SustainState.Hitting;
        }

        public virtual void MissNote()
        {
            SustainState = SustainState.Missed;
        }

        public virtual void SustainEnd()
        {
            SustainState = SustainState.Missed;
        }

        protected virtual void HideNotes()
        {
            foreach (var note in NoteGroups)
            {
                if (note == null) return;

                note.SetActive(false);
            }

            foreach (var note in StarPowerNoteGroups)
            {
                if (note == null) return;

                note.SetActive(false);
            }
        }

        protected NoteGroup CreateNoteGroupFromTheme(GameObject themeModel)
        {
            var noteObj = new GameObject("Note Group");
            var noteTransform = noteObj.transform;

            noteTransform.parent = transform;
            noteTransform.localPosition = Vector3.zero;

            var noteGroup = noteObj.AddComponent<NoteGroup>();
            noteGroup.SetModelFromTheme(themeModel);

            return noteGroup;
        }

        protected void CreateNoteGroupArrays(int len)
        {
            NoteGroups = new NoteGroup[len];
            StarPowerNoteGroups = new NoteGroup[len];
        }

        protected void AssignNoteGroup(ThemeDict models, ThemeDict starpowerModels,
            int index, ThemeNoteType noteType)
        {
            var normalNote = CreateNoteGroupFromTheme(models[noteType]);
            NoteGroups[index] = normalNote;

            if (starpowerModels.TryGetValue(noteType, out var starpowerModel))
            {
                StarPowerNoteGroups[index] = CreateNoteGroupFromTheme(starpowerModel);
            }
            else
            {
                StarPowerNoteGroups[index] = normalNote;
            }
        }
    }
}