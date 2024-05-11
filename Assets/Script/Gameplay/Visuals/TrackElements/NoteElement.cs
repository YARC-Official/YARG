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

        public abstract void SetThemeModels(ThemeDict models, ThemeDict starPowerModels);

        protected override void InitializeElement()
        {
            SustainState = SustainState.Waiting;
            _lastStarPowerState = NoteRef.IsStarPower;
        }

        protected override void UpdateElement()
        {
            // Call OnStarPowerUpdated if the star power state of the note changes
            if (_lastStarPowerState != NoteRef.IsStarPower)
            {
                OnStarPowerUpdated();
                _lastStarPowerState = NoteRef.IsStarPower;
            }
        }

        /// <summary>
        /// Called whenever the star power state of the note changes.
        /// May also be called when the player activates or gains star power,
        /// depending on the player class.
        /// </summary>
        public virtual void OnStarPowerUpdated()
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
                NoteGroup.Initialize();
            }
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