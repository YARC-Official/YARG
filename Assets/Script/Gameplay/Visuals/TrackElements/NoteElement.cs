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

    public abstract class NoteElement<TNote, TPlayer> : TrackElement<TPlayer>, IThemeNoteCreator
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
        private bool _lastStarPowerActiveState;

        protected bool IsStarPowerVisible;

        public abstract void SetThemeModels(ThemeDict models, ThemeDict starPowerModels);

        protected override void InitializeElement()
        {
            SustainState = SustainState.Waiting;
            _lastStarPowerState = NoteRef.IsStarPower;
            _lastStarPowerActiveState = Player.BaseStats.IsStarPowerActive;

            IsStarPowerVisible = CalcStarPowerVisible();
        }

        protected override void UpdateElement()
        {
            // Call OnStarPowerUpdated if the star power state of the note or star power activation changes
            if (_lastStarPowerState != NoteRef.IsStarPower || _lastStarPowerActiveState != Player.BaseStats.IsStarPowerActive)
            {
                OnStarPowerUpdated();
            }
            _lastStarPowerState = NoteRef.IsStarPower;
            _lastStarPowerActiveState = Player.BaseStats.IsStarPowerActive;
        }

        protected virtual bool CalcStarPowerVisible()
        {
            return NoteRef.IsStarPower;
        }

        /// <summary>
        /// Called whenever the star power state of the note changes, or when the player star power activation changes.
        /// </summary>
        public virtual void OnStarPowerUpdated()
        {
            bool shouldShowStarPower = CalcStarPowerVisible();
            // If visible star power state didn't change, skip
            if (IsStarPowerVisible == shouldShowStarPower) return;

            // If we did have star power and the user lost it (or vice versa), then swap the model out
            int index = Array.IndexOf(IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups, NoteGroup);
            if (index != -1)
            {
                // Disable the old note group
                NoteGroup.SetActive(false);

                // Enable the new one
                NoteGroup = (IsStarPowerVisible ? NoteGroups : StarPowerNoteGroups)[index];
                NoteGroup.SetActive(true);
                NoteGroup.Initialize();

            }
            IsStarPowerVisible = shouldShowStarPower;
        }

        /// <summary>
        /// Called when the player hits this note.
        /// </summary>
        public virtual void HitNote()
        {
            SustainState = SustainState.Hitting;
            OnNoteStateChanged();
        }

        /// <summary>
        /// Called when the player misses this note.
        /// </summary>
        public virtual void MissNote()
        {
            SustainState = SustainState.Missed;
            OnNoteStateChanged();
        }

        /// <summary>
        /// Called when the sustain of this note ends.
        /// </summary>
        /// <param name="finished">Whether or not the sustain was dropped before it finished.</param>
        public virtual void SustainEnd(bool finished)
        {
            SustainState = SustainState.Missed;
            OnNoteStateChanged();
        }

        /// <summary>
        /// Called when the state of this note changes.
        /// </summary>
        protected virtual void OnNoteStateChanged()
        {
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

        protected void CreateNoteGroupArrays(int len)
        {
            NoteGroups = new NoteGroup[len];
            StarPowerNoteGroups = new NoteGroup[len];
        }

        protected void AssignNoteGroup(ThemeDict models, ThemeDict starPowerModels,
            int index, ThemeNoteType noteType)
        {
            var normalNote = NoteGroup.CreateNoteGroupFromTheme(transform, models[noteType]);
            NoteGroups[index] = normalNote;

            if (starPowerModels.TryGetValue(noteType, out var starPowerModel))
            {
                StarPowerNoteGroups[index] = NoteGroup.CreateNoteGroupFromTheme(transform, starPowerModel);
            }
            else
            {
                StarPowerNoteGroups[index] = normalNote;
            }
        }
    }
}