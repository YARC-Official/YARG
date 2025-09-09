using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;
using Color = System.Drawing.Color;

namespace YARG.Gameplay.Visuals
{
    public sealed class SixFretNoteElement : NoteElement<GuitarNote, SixFretPlayer>
    {
        private enum NoteType
        {
            Strum    = 0,
            HOPO     = 1,
            Tap      = 2,
            Open     = 3,
            OpenHOPO = 4,
            Bar      = 5,
            BarHOPO  = 6,
            BarTap   = 7,
            W        = 8,
            WHOPO    = 9,
            WTap     = 10,
            B        = 11,
            BHOPO    = 12,
            BTap     = 13,
            Count
        }

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;

        private SustainLine _sustainLine;

        // NEW: set by SixFretPlayer (unique display indices for the chord)
        public List<int> DisplayFrets { get; set; }

        // If this instance is not the chosen visual representation for its display index,
        // we'll keep it invisible but still allow hit/miss lifecycle methods to run.
        private bool _isPrimaryVisual = true;

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
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Bar,      ThemeNoteType.Bar);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.BarHOPO,  ThemeNoteType.BarHOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.BarTap,   ThemeNoteType.BarTap);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.W,        ThemeNoteType.W);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.WHOPO,    ThemeNoteType.WHOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.WTap,     ThemeNoteType.WTap);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.B,        ThemeNoteType.B);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.BHOPO,    ThemeNoteType.BHOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.BTap,     ThemeNoteType.BTap);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            // Determine siblings (notes in the same parent/chord)
            var siblingNotes = new List<GuitarNote>();
            if (NoteRef.Parent != null)
            {
                foreach (var n in NoteRef.Parent.AllNotes)
                    siblingNotes.Add(n);
            }
            else
            {
                siblingNotes.Add(NoteRef);
            }

            // Group siblings by display index (0..2) using same mapping as SixFretPlayer
            var groups = new Dictionary<int, List<GuitarNote>>();
            foreach (var n in siblingNotes)
            {
                int displayIdx = GetDisplayIndexForFret(n.Fret);
                if (!groups.ContainsKey(displayIdx))
                    groups[displayIdx] = new List<GuitarNote>();
                groups[displayIdx].Add(n);
            }

            // Which display index does THIS note occupy?
            int myDisplayIndex = GetDisplayIndexForFret(NoteRef.Fret);

            // Choose the primary note instance for this display index.
            // Heuristic: choose the note with the smallest Fret number.
            List<GuitarNote> myGroup = groups.ContainsKey(myDisplayIndex) ? groups[myDisplayIndex] : new List<GuitarNote> { NoteRef };
            GuitarNote primaryNote = myGroup[0];
            for (int i = 1; i < myGroup.Count; i++)
            {
                if (myGroup[i].Fret < primaryNote.Fret)
                    primaryNote = myGroup[i];
            }

            // If this instance is not the primary visual for the display index, disable visual parts.
            if (!ReferenceEquals(primaryNote, NoteRef))
            {
                _isPrimaryVisual = false;

                // Make sure visuals are not shown.
                var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;
                if (noteGroups != null)
                {
                    foreach (var g in noteGroups)
                        if (g != null)
                            g.SetActive(false);
                }

                if (_normalSustainLine != null)
                    _normalSustainLine.gameObject.SetActive(false);
                if (_openSustainLine != null)
                    _openSustainLine.gameObject.SetActive(false);

                // we intentionally DO NOT return here, because other pieces of the lifecycle (HitNote, etc.)
                // may still be called on this object. But we skip visual initialization below.
                return;
            }

            // PRIMARY - set up visuals for this display index.
            var activeNoteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            // Count how many notes fall in the same display lane as this note (myDisplayIndex)
            int notesInThisLane = myGroup.Count;

            // Check if this is a bar chord by looking at the parent chord
            bool isBarChord = false;
            if (NoteRef.Parent != null)
            {
                var allSiblingNotes = new List<GuitarNote>();
                foreach (var n in NoteRef.Parent.AllNotes)
                {
                    allSiblingNotes.Add(n);
                }
                
                // Check if there are multiple notes that would map to the same display position
                var siblingGroups = new Dictionary<int, List<GuitarNote>>();
                foreach (var n in allSiblingNotes)
                {
                    int displayIdx = GetDisplayIndexForFret(n.Fret);
                    if (!siblingGroups.ContainsKey(displayIdx))
                        siblingGroups[displayIdx] = new List<GuitarNote>();
                    siblingGroups[displayIdx].Add(n);
                }
                
                // Check if this note is in a same-lane bar chord (multiple notes in same lane)
                int currentDisplayIndex = GetDisplayIndexForFret(NoteRef.Fret);
                if (siblingGroups.ContainsKey(currentDisplayIndex) && siblingGroups[currentDisplayIndex].Count > 1)
                {
                    // This is a same-lane bar chord - all notes in this lane should be bars
                    isBarChord = true;
                }
                // For cross-lane chords, only make the primary note (lowest fret) a bar note
                else if (siblingGroups.Count > 1)
                {
                    // Find the primary note (lowest fret number) in the chord
                    var chordPrimaryNote = allSiblingNotes.OrderBy(n => n.Fret).First();
                    // Only make this note a bar if it's the primary note
                    isBarChord = (NoteRef == chordPrimaryNote);
                }
            }

            // Bar note if more than one note in this lane OR if this is a bar chord
            bool isBar = notesInThisLane > 1 || isBarChord;

            // Choose the note type based on whether it's a bar or single and the original GuitarNoteType
            NoteType noteType = NoteType.Strum;
            if (isBar)
            {
                switch (NoteRef.Type)
                {
                    case GuitarNoteType.Strum: noteType = NoteType.Bar; break;
                    case GuitarNoteType.Hopo:  noteType = NoteType.BarHOPO; break;
                    case GuitarNoteType.Tap:   noteType = NoteType.BarTap; break;
                }
            }
            else
            {
                // Single note - determine color by actual fret number (1-3 => B, 4-6 => W)
                if (NoteRef.Fret >= 1 && NoteRef.Fret <= 3)
                {
                    switch (NoteRef.Type)
                    {
                        case GuitarNoteType.Strum: noteType = NoteType.B; break;
                        case GuitarNoteType.Hopo:  noteType = NoteType.BHOPO; break;
                        case GuitarNoteType.Tap:   noteType = NoteType.BTap; break;
                    }
                }
                else if (NoteRef.Fret >= 4 && NoteRef.Fret <= 6)
                {
                    switch (NoteRef.Type)
                    {
                        case GuitarNoteType.Strum: noteType = NoteType.W; break;
                        case GuitarNoteType.Hopo:  noteType = NoteType.WHOPO; break;
                        case GuitarNoteType.Tap:   noteType = NoteType.WTap; break;
                    }
                }
                else
                {
                    // Fallback to open/tap types
                    switch (NoteRef.Type)
                    {
                        case GuitarNoteType.Strum: noteType = NoteType.Open; break;
                        case GuitarNoteType.Hopo:  noteType = NoteType.OpenHOPO; break;
                        case GuitarNoteType.Tap:   noteType = NoteType.Tap; break;
                    }
                }
            }

            // Deactivate all groups first
            if (activeNoteGroups != null)
            {
                foreach (var g in activeNoteGroups)
                {
                    if (g != null)
                        g.SetActive(false);
                }
            }

            // Position the visual by display index (0..2)
            int lane = myDisplayIndex;
            float x = TrackPlayer.TRACK_WIDTH / 3f * lane - TrackPlayer.TRACK_WIDTH / 2f + 1f / 3f;
            transform.localPosition = new Vector3(x, 0f, 0f) * LeftyFlipMultiplier;

            // Activate/initialize the chosen group
            NoteGroup = activeNoteGroups[(int)noteType];
            _sustainLine = (NoteRef.Fret == (int)SixFretGuitarFret.Open) ? _openSustainLine : _normalSustainLine;

            if (NoteGroup != null)
            {
                NoteGroup.SetActive(true);
                NoteGroup.Initialize();
            }

            if (NoteRef.IsSustain)
            {
                if (_sustainLine != null)
                {
                    _sustainLine.gameObject.SetActive(true);
                    float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                    _sustainLine.Initialize(len);
                }
            }

            UpdateColor();
        }

        /// <summary>
        /// Maps a 6-fret number to a 0–2 display index.
        /// B1 -> 0, B2 -> 1, B3 -> 2, W4 -> 0, W5 -> 1, W6 -> 2
        /// </summary>
        private static int GetDisplayIndexForFret(int fret)
        {
            if (fret >= 1 && fret <= 6)
            {
                return (fret - 1) % 3;
            }
            return 0;
        }

        /// <summary>
        /// Checks if the group contains a bar chord combination (B1+W4, B2+W5, B3+W6)
        /// </summary>
        private static bool IsBarChordCombination(List<GuitarNote> group)
        {
            if (group.Count != 2)
                return false;

            var frets = group.Select(n => n.Fret).OrderBy(f => f).ToList();
            
            // Check for bar chord combinations: (B1,W4), (B2,W5), (B3,W6)
            return (frets[0] == 1 && frets[1] == 4) ||
                   (frets[0] == 2 && frets[1] == 5) ||
                   (frets[0] == 3 && frets[1] == 6);
        }

        public override void HitNote()
        {
            base.HitNote();

            // For sustains we hide visuals; for non-primary elements (hidden visuals) we still return to pool.
            if (NoteRef.IsSustain)
            {
                HideNotes();
            }
            else
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
            base.UpdateElement();
            if (_sustainLine != null)
                _sustainLine.UpdateSustainLine(Player.NoteSpeed * GameManager.SongSpeed);
        }

        protected override void OnNoteStateChanged()
        {
            base.OnNoteStateChanged();
            // Only update color if this is the primary visual
            if (_isPrimaryVisual)
                UpdateColor();
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();
            if (_isPrimaryVisual)
                UpdateColor();
        }

        private void UpdateColor()
        {
            if (NoteGroup == null)
                return;

            Color colorNoStarPower;

            // If this is a bar (multiple notes on the display index), we use the bar group's color mapping,
            // otherwise use black/white mapping based on the primary note's fret.
            if (NoteRef.Fret >= 1 && NoteRef.Fret <= 3)
            {
                colorNoStarPower = System.Drawing.Color.Black;
            }
            else if (NoteRef.Fret >= 4 && NoteRef.Fret <= 6)
            {
                colorNoStarPower = System.Drawing.Color.White;
            }
            else
            {
                colorNoStarPower = System.Drawing.Color.Black; // default
            }

            var color = NoteRef.IsStarPower
                ? System.Drawing.Color.Cyan
                : colorNoStarPower;

            NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());

            if (!NoteRef.IsSustain || _sustainLine == null) return;
            _sustainLine.SetState(SustainState, color.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();
            if (_normalSustainLine != null) _normalSustainLine.gameObject.SetActive(false);
            if (_openSustainLine != null) _openSustainLine.gameObject.SetActive(false);
        }
    }
}
