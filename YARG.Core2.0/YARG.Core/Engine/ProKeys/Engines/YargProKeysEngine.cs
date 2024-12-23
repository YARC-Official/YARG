using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.ProKeys.Engines
{
    public class YargProKeysEngine : ProKeysEngine
    {
        public YargProKeysEngine(InstrumentDifficulty<ProKeysNote> chart, SyncTrack syncTrack,
            ProKeysEngineParameters engineParameters, bool isBot, SongChart FullChart) : base(chart, syncTrack, engineParameters, isBot, FullChart)
        {
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<ProKeysAction>();

            if (action is ProKeysAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
            else if (action is ProKeysAction.TouchEffects)
            {
                StarPowerWhammyTimer.Start(gameInput.Time);
            }
            else
            {
                if (gameInput.Button)
                {
                    KeyHit = (int) action;
                }
                else
                {
                    KeyReleased = (int) action;
                }

                PreviousKeyMask = KeyMask;
                ToggleKey((int) action, gameInput.Button);
                KeyPressTimes[(int) action] = gameInput.Time;

                OnKeyStateChange?.Invoke((int) action, gameInput.Button);
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();

            // Update bot (will return if not enabled)
            UpdateBot(time);

            if (FatFingerTimer.IsActive)
            {
                // Fat Fingered key was released before the timer expired
                if (KeyReleased == FatFingerKey && !FatFingerTimer.IsExpired(CurrentTime))
                {
                    YargLogger.LogFormatTrace("Released fat fingered key at {0}. Note was hit: {1}", CurrentTime, FatFingerNote!.WasHit);

                    // The note must be hit to disable the timer
                    if (FatFingerNote!.WasHit)
                    {
                        YargLogger.LogDebug("Disabling fat finger timer as the note has been hit.");
                        FatFingerTimer.Disable();
                        FatFingerKey = null;
                        FatFingerNote = null;
                    }
                }
                else if(FatFingerTimer.IsExpired(CurrentTime))
                {
                    YargLogger.LogFormatTrace("Fat Finger timer expired at {0}", CurrentTime);

                    var fatFingerKeyMask = 1 << FatFingerKey;

                    var isHoldingWrongKey = (KeyMask & fatFingerKeyMask) == fatFingerKeyMask;

                    // Overhit if key is still held OR if key is not held but note was not hit either
                    if (isHoldingWrongKey || (!isHoldingWrongKey && !FatFingerNote!.WasHit))
                    {
                        YargLogger.LogFormatTrace("Overhit due to fat finger with key {0}. KeyMask: {1}. Holding: {2}. WasHit: {3}",
                            FatFingerKey, KeyMask, isHoldingWrongKey, FatFingerNote!.WasHit);
                        Overhit(FatFingerKey!.Value);
                    }

                    FatFingerTimer.Disable();
                    FatFingerKey = null;
                    FatFingerNote = null;
                }
            }

            // Quit early if there are no notes left
            if (NoteIndex >= Notes.Count)
            {
                KeyHit = null;
                KeyReleased = null;
                UpdateSustains();
                return;
            }

            CheckForNoteHit();
            UpdateSustains();
        }

        protected override void CheckForNoteHit()
        {
            var parentNote = Notes[NoteIndex];

            // Miss out the back end
            if (!IsNoteInWindow(parentNote, out bool missed))
            {
                if (missed)
                {
                    // If one of the notes in the chord was missed out the back end,
                    // that means all of them would miss.
                    foreach (var missedNote in parentNote.AllNotes)
                    {
                        MissNote(missedNote);
                    }
                }
            }
            else
            {
                double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(parentNote));
                double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);
                double backEnd = EngineParameters.HitWindow.GetBackEnd(hitWindow);

                // Hit whole note
                if (CanNoteBeHit(parentNote))
                {
                    foreach (var childNote in parentNote.AllNotes)
                    {
                        HitNote(childNote);
                    }

                    KeyHit = null;
                }
                else
                {
                    // Note cannot be hit in full, try to use chord staggering logic

                    if (parentNote.IsChord)
                    {
                        // Note is a chord and chord staggering was active and is now expired
                        if (ChordStaggerTimer.IsActive && ChordStaggerTimer.IsExpired(CurrentTime))
                        {
                            YargLogger.LogFormatTrace("Ending chord staggering at {0}", CurrentTime);
                            foreach (var note in parentNote.AllNotes)
                            {
                                // This key in the chord was held by the time chord staggering ended, so it can be hit
                                if ((KeyMask & note.DisjointMask) == note.DisjointMask && IsKeyInTime(note, frontEnd))
                                {
                                    HitNote(note);
                                    YargLogger.LogFormatTrace("Hit staggered note {0} in chord", note.Key);
                                }
                                else
                                {
                                    YargLogger.LogFormatTrace("Missing note {0} due to chord staggering", note.Key);
                                    MissNote(note);
                                }
                            }

                            ChordStaggerTimer.Disable();
                        }
                        else
                        {
                            foreach (var note in parentNote.AllNotes)
                            {
                                // Go to next note if the key hit does not match the note's key
                                if (KeyHit != note.Key)
                                {
                                    continue;
                                }

                                if (!ChordStaggerTimer.IsActive)
                                {
                                    StartTimer(ref ChordStaggerTimer, CurrentTime);
                                    YargLogger.LogFormatTrace("Starting chord staggering at {0}. End time is {1}",
                                        CurrentTime, ChordStaggerTimer.EndTime);

                                    var chordStaggerEndTime = ChordStaggerTimer.EndTime;

                                    double noteMissTime = note.Time + backEnd;

                                    // Time has surpassed the back end of this note
                                    if (chordStaggerEndTime > noteMissTime)
                                    {
                                        double diff = noteMissTime - chordStaggerEndTime;
                                        StartTimer(ref ChordStaggerTimer, CurrentTime - Math.Abs(diff));

                                        YargLogger.LogFormatTrace(
                                            "Chord stagger window shortened by {0}. New end time is {1}. Note backend time is {2}",
                                            diff, ChordStaggerTimer.EndTime, noteMissTime);
                                    }
                                }

                                KeyHit = null;
                                break;
                            }
                        }
                    }
                }
            }

            // If no note was hit but the user hit a key, then over hit
            if (KeyHit != null)
            {
                static ProKeysNote? CheckForAdjacency(ProKeysNote fullNote, int key)
                {
                    foreach (var note in fullNote.AllNotes)
                    {
                        if (ProKeysUtilities.IsAdjacentKey(note.Key, key))
                        {
                            return note;
                        }
                    }

                    return null;
                }

                ProKeysNote? adjacentNote;
                bool isAdjacent;
                bool inWindow;

                // Try to fat finger previous note first

                // Previous note can only be fat fingered if the current distance from the note
                // is within the fat finger threshold (default 100ms)
                if (parentNote.PreviousNote is not null
                    && CurrentTime - parentNote.PreviousNote.Time < FatFingerTimer.SpeedAdjustedThreshold)
                {
                    adjacentNote = CheckForAdjacency(parentNote.PreviousNote, KeyHit.Value);
                    isAdjacent = adjacentNote != null;
                    inWindow = IsNoteInWindow(parentNote.PreviousNote, out _);

                }
                // Try to fat finger current note (upcoming note)
                else
                {
                    adjacentNote = CheckForAdjacency(parentNote, KeyHit.Value);
                    isAdjacent = adjacentNote != null;
                    inWindow = IsNoteInWindow(parentNote, out _);
                }

                var isFatFingerActive = FatFingerTimer.IsActive;

                if (!inWindow || !isAdjacent || isFatFingerActive)
                {
                    Overhit(KeyHit.Value);

                    // TODO Maybe don't disable the timer/use a flag saying no more fat fingers allowed for the current note.

                    FatFingerTimer.Disable();
                    FatFingerKey = null;
                    FatFingerNote = null;
                }
                else
                {
                    StartTimer(ref FatFingerTimer, CurrentTime);
                    FatFingerKey = KeyHit.Value;

                    FatFingerNote = adjacentNote;

                    YargLogger.LogFormatTrace("Hit adjacent key {0} to note {1}. Starting fat finger timer at {2}. End time: {3}. Key is {4}", FatFingerKey, adjacentNote!.Key, CurrentTime,
                        FatFingerTimer.EndTime, FatFingerKey);
                }

                KeyHit = null;
            }
        }

        protected override bool CanNoteBeHit(ProKeysNote note)
        {
            double hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            double frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

            if((KeyMask & note.NoteMask) == note.NoteMask)
            {
                foreach (var childNote in note.AllNotes)
                {
                    if (!IsKeyInTime(childNote, frontEnd))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Glissando hit logic
            // Forces the first glissando to be hit correctly, then the rest can be hit "loosely"
            if (note.PreviousNote is not null && note.IsGlissando && note.PreviousNote.IsGlissando)
            {
                var keyDiff = KeyMask ^ PreviousKeyMask;
                var keysPressed = keyDiff & KeyMask;
                //var keysReleased = keyDiff & PreviousKeyMask;

                foreach (var child in note.AllNotes)
                {
                    var pressCopy = keysPressed;

                    int i = 0;
                    while (pressCopy > 0)
                    {
                        if((pressCopy & 1) != 0 && IsKeyInTime(child, i, frontEnd))
                        {
                            // It's not ideal that this is here but there's no way to know what key hit the note
                            // within HitNote() so we have to set the press time here
                            KeyPressTimes[i] = DEFAULT_PRESS_TIME;
                            return true;
                        }

                        i++;
                        pressCopy >>= 1;
                    }
                }
            }

            return false;
        }

        protected override void UpdateBot(double time)
        {
            if (!IsBot || NoteIndex >= Notes.Count)
            {
                return;
            }

            var note = Notes[NoteIndex];

            if (time < note.Time)
            {
                return;
            }

            // Disables keys that are not in the current note
            int key = 0;
            for (var mask = KeyMask; mask > 0; mask >>= 1)
            {
                if ((mask & 1) == 1)
                {
                    MutateStateWithInput(new GameInput(note.Time, key, false));
                }

                key++;
            }


            // Press keys for current note
            foreach (var chordNote in note.AllNotes)
            {
                MutateStateWithInput(new GameInput(note.Time, chordNote.Key, true));
                CheckForNoteHit();
            }
        }
    }
}