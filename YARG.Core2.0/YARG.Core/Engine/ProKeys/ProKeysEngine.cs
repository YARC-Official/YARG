using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys
{
    public abstract class ProKeysEngine : BaseEngine<ProKeysNote, ProKeysEngineParameters,
        ProKeysStats>
    {
        protected const double DEFAULT_PRESS_TIME = -9999;

        public delegate void KeyStateChangeEvent(int key, bool isPressed);
        public delegate void OverhitEvent(int key);

        public KeyStateChangeEvent? OnKeyStateChange;

        public OverhitEvent? OnOverhit;

        // Used for hit logic. May not be the same value as KeyHeldMask
        public int KeyMask { get; protected set; }

        public int PreviousKeyMask { get; protected set; }

        protected double[] KeyPressTimes = new double[(int)ProKeysAction.Key25 + 1];

        /// <summary>
        /// The integer value for the key that was hit this update. <c>null</c> is none.
        /// </summary>
        protected int? KeyHit;

        /// <summary>
        /// The integer value for the key that was released this update. <c>null</c> is none.
        /// </summary>
        protected int? KeyReleased;

        protected int? FatFingerKey;

        protected EngineTimer ChordStaggerTimer;
        protected EngineTimer FatFingerTimer;

        protected ProKeysNote? FatFingerNote;

        protected ProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot, SongChart FullChart)
            : base(chart, syncTrack, engineParameters, true, isBot, FullChart)
        {
            ChordStaggerTimer = new(engineParameters.ChordStaggerWindow);
            FatFingerTimer = new(engineParameters.FatFingerWindow);

            KeyPressTimes = new double[(int)ProKeysAction.Key25 + 1];
            for(int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }

            GetWaitCountdowns(Notes);
        }

        public EngineTimer GetChordStaggerTimer() => ChordStaggerTimer;
        public EngineTimer GetFatFingerTimer() => FatFingerTimer;

        public ReadOnlySpan<double> GetKeyPressTimes() => KeyPressTimes;

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = CurrentTime;

            if (ChordStaggerTimer.IsActive)
            {
                if (IsTimeBetween(ChordStaggerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing chord stagger end time at {0}", ChordStaggerTimer.EndTime);
                    QueueUpdateTime(ChordStaggerTimer.EndTime, "Chord Stagger End");
                }
            }

            if (FatFingerTimer.IsActive)
            {
                if (IsTimeBetween(FatFingerTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing fat finger end time at {0}", FatFingerTimer.EndTime);
                    QueueUpdateTime(FatFingerTimer.EndTime, "Fat Finger End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            KeyMask = 0;

            for(int i = 0; i < KeyPressTimes.Length; i++)
            {
                KeyPressTimes[i] = -9999;
            }

            KeyHit = null;
            KeyReleased = null;

            FatFingerKey = null;

            ChordStaggerTimer.Disable();
            FatFingerTimer.Disable();

            FatFingerNote = null;

            base.Reset(keepCurrentButtons);
        }

        protected virtual void Overhit(int key)
        {
            // Can't overstrum before first note is hit/missed
            if (NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Cancel overstrum if WaitCountdown is active
            if (IsWaitCountdownActive)
            {
                YargLogger.LogFormatTrace("Overstrum prevented during WaitCountdown at time: {0}, tick: {1}", CurrentTime, CurrentTick);
                return;
            }

            YargLogger.LogFormatTrace("Overhit at {0}", CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), CurrentTime);
                i--;

                double finalScore = CalculateSustainPoints(ref sustain, CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, CurrentTime, sustain.HasFinishedScoring);
            }

            if (NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[NoteIndex]);
                }
            }

            EngineStats.Combo = 0;
            EngineStats.Overhits++;

            UpdateMultiplier();

            OnOverhit?.Invoke(key);
        }

        protected override bool CanSustainHold(ProKeysNote note)
        {
            return (KeyMask & note.DisjointMask) != 0;
        }

        protected override void HitNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            bool partiallyHit = false;
            foreach(var child in note.ParentOrSelf.AllNotes)
            {
                if (child.WasHit || child.WasMissed)
                {
                    partiallyHit = true;
                    break;
                }
            }

            note.SetHitState(true, false);

            KeyPressTimes[note.Key] = DEFAULT_PRESS_TIME;

            // Detect if the last note(s) were skipped
            // bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd && note.ParentOrSelf.WasFullyHit())
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (IsSoloActive)
            {
                Solos[CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (note.ParentOrSelf.WasFullyHit())
            {
                ChordStaggerTimer.Disable();
            }

            // Only increase combo for the first note in a chord
            if (!partiallyHit)
            {
                EngineStats.Combo++;

                if (EngineStats.Combo > EngineStats.MaxCombo)
                {
                    EngineStats.MaxCombo = EngineStats.Combo;
                }
            }

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            if (note.IsSustain)
            {
                StartSustain(note);
            }

            OnNoteHit?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(ProKeysNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Key: {0}, Index: {1}, Hit: {2}, Missed: {3})",
                    note.Key, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetMissState(true, false);

            KeyPressTimes[note.Key] = DEFAULT_PRESS_TIME;

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd && note.ParentOrSelf.WasFullyHitOrMissed())
            {
                EndSolo();
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            // If no notes within a chord were hit, combo is 0
            if (note.ParentOrSelf.WasFullyMissed())
            {
                EngineStats.Combo = 0;
            }
            else
            {
                // If any of the notes in a chord were hit, the combo for that note is rewarded, but it is reset back to 1
                EngineStats.Combo = 1;
            }

            UpdateMultiplier();

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void AddScore(ProKeysNote note)
        {
            AddScore(POINTS_PER_PRO_NOTE);
            EngineStats.NoteScore += POINTS_PER_NOTE;
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_PRO_NOTE * (1 + note.ChildNotes.Count);

                foreach (var child in note.AllNotes)
                {
                    score += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                }
            }

            return score;
        }

        protected void ToggleKey(int key, bool active)
        {
            KeyMask = active ? KeyMask | (1 << key) : KeyMask & ~(1 << key);
        }

        protected bool IsKeyInTime(ProKeysNote note, int key, double frontEnd)
        {
            return KeyPressTimes[key] > note.Time + frontEnd;
        }

        protected bool IsKeyInTime(ProKeysNote note, double frontEnd) => IsKeyInTime(note, note.Key, frontEnd);
    }
}