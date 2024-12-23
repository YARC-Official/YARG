using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {
        public YargFiveFretEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot, SongChart FullChart)
            : base(chart, syncTrack, engineParameters, isBot, FullChart)
        {
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

            LastButtonMask = ButtonMask;
            ButtonMask = (byte) note.NoteMask;

            YargLogger.LogFormatTrace("[Bot] Set button mask to: {0}", ButtonMask);

            HasTapped = ButtonMask != LastButtonMask;
            IsFretPress = true;
            HasStrummed = false;
            StrumLeniencyTimer.Start(time);

            foreach (var sustain in ActiveSustains)
            {
                var sustainNote = sustain.Note;

                if (!sustainNote.IsExtendedSustain)
                {
                    continue;
                }

                if (sustainNote.IsDisjoint)
                {
                    ButtonMask |= (byte) sustainNote.DisjointMask;

                    YargLogger.LogFormatTrace("[Bot] Added Disjoint Sustain Mask {0} to button mask. {1}", sustainNote.DisjointMask, ButtonMask);
                }
                else
                {
                    ButtonMask |= (byte) sustainNote.NoteMask;

                    YargLogger.LogFormatTrace("[Bot] Added Sustain Mask {0} to button mask. {1}", sustainNote.NoteMask, ButtonMask);
                }
            }
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<GuitarAction>();

            // Star power
            if (action is GuitarAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
            else if (action is GuitarAction.Whammy)
            {
                StarPowerWhammyTimer.Start(gameInput.Time);
            }
            else if (action is GuitarAction.StrumDown or GuitarAction.StrumUp && gameInput.Button)
            {
                HasStrummed = true;
            }
            else if (IsFretInput(gameInput))
            {
                LastButtonMask = ButtonMask;
                HasFretted = true;
                IsFretPress = gameInput.Button;

                ToggleFret(gameInput.Action, gameInput.Button);

                // No other frets are held, enable the "open fret"
                if ((ButtonMask & ~OPEN_MASK) == 0)
                {
                    ButtonMask |= OPEN_MASK;
                }
                else
                {
                    // Some frets are held, disable the "open fret"
                    ButtonMask &= unchecked((byte) ~OPEN_MASK);
                }
            }

            YargLogger.LogFormatTrace("Mutated input state: Button Mask: {0}, HasFretted: {1}, HasStrummed: {2}",
                ButtonMask, HasFretted, HasStrummed);
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();
            UpdateTimers();

            bool strumEatenByHopo = false;

            // This is up here so overstrumming still works when there are no notes left
            if (HasStrummed)
            {
                // Hopo was hit recently, eat strum input
                if (HopoLeniencyTimer.IsActive)
                {
                    StrumLeniencyTimer.Disable();

                    // Disable hopo leniency as hopos can only eat one strum
                    HopoLeniencyTimer.Disable();

                    strumEatenByHopo = true;
                    ReRunHitLogic = true;
                }
                else
                {
                    // Strummed while strum leniency is active (double strum)
                    if (StrumLeniencyTimer.IsActive)
                    {
                        Overstrum();
                    }
                }

                if (!strumEatenByHopo)
                {
                    double offset = 0;

                    if (NoteIndex >= Notes.Count || !IsNoteInWindow(Notes[NoteIndex]))
                    {
                        offset = EngineParameters.StrumLeniencySmall;
                    }

                    StartTimer(ref StrumLeniencyTimer, CurrentTime, offset);

                    ReRunHitLogic = true;
                }
            }

            // Update bot (will return if not enabled)
            UpdateBot(time);

            // Quit early if there are no notes left
            if (NoteIndex >= Notes.Count)
            {
                HasStrummed = false;
                HasFretted = false;
                IsFretPress = false;
                UpdateSustains();
                return;
            }

            var note = Notes[NoteIndex];

            var hitWindow = EngineParameters.HitWindow.CalculateHitWindow(GetAverageNoteDistance(note));
            var frontEnd = EngineParameters.HitWindow.GetFrontEnd(hitWindow);

            if (HasFretted)
            {
                HasTapped = true;

                // This is the time the front end will expire. Used for hit logic with infinite front end
                FrontEndExpireTime = CurrentTime + Math.Abs(frontEnd);

                // Check for fret ghosting
                // We want to run ghost logic regardless of the setting for the ghost counter
                bool ghosted = CheckForGhostInput(note);

                // This variable controls hit logic for ghosting
                WasNoteGhosted = EngineParameters.AntiGhosting && (ghosted || WasNoteGhosted);

                // Add ghost inputs to stats regardless of the setting for anti ghosting
                if (ghosted)
                {
                    EngineStats.GhostInputs++;
                }
            }

            CheckForNoteHit();
            UpdateSustains();

            HasStrummed = false;
            HasFretted = false;
            IsFretPress = false;
        }

        protected override void CheckForNoteHit()
        {
            for (int i = NoteIndex; i < Notes.Count; i++)
            {
                bool isFirstNoteInWindow = i == NoteIndex;
                var note = Notes[i];

                if (note.WasFullyHitOrMissed())
                {
                    break;
                }

                if (!IsNoteInWindow(note, out bool missed))
                {
                    if (isFirstNoteInWindow && missed)
                    {
                        MissNote(note);
                        YargLogger.LogFormatTrace("Missed note (Index: {0}, Mask: {1}) at {2}", i,
                            note.NoteMask, CurrentTime);
                    }

                    break;
                }

                // Cannot hit the note
                if (!CanNoteBeHit(note))
                {
                    YargLogger.LogFormatTrace("Cant hit note (Index: {0}, Mask {1}) at {2}. Buttons: {3}", i,
                        note.NoteMask, CurrentTime, ButtonMask);
                    // This does nothing special, it's just logging strum leniency
                    if (isFirstNoteInWindow && HasStrummed && StrumLeniencyTimer.IsActive)
                    {
                        YargLogger.LogFormatTrace("Starting strum leniency at {0}, will end at {1}", CurrentTime,
                            StrumLeniencyTimer.EndTime);
                    }

                    // Note skipping not allowed on the first note if hopo/tap
                    if ((note.IsHopo || note.IsTap) && NoteIndex == 0)
                    {
                        break;
                    }

                    // Continue to the next note (skipping the current one)
                    continue;
                }

                // Handles hitting a hopo notes
                // If first note is a hopo then it can be hit without combo (for practice mode)
                bool hopoCondition = note.IsHopo && isFirstNoteInWindow &&
                    (EngineStats.Combo > 0 || NoteIndex == 0);

                // If a note is a tap then it can be hit only if it is the closest note, unless
                // the combo is 0 then it can be hit regardless of the distance (note skipping)
                bool tapCondition = note.IsTap && (isFirstNoteInWindow || EngineStats.Combo == 0);

                bool frontEndIsExpired = note.Time > FrontEndExpireTime;
                bool canUseInfFrontEnd =
                    EngineParameters.InfiniteFrontEnd || !frontEndIsExpired || NoteIndex == 0;

                // Attempt to hit with hopo/tap rules
                if (HasTapped && (hopoCondition || tapCondition) && canUseInfFrontEnd && !WasNoteGhosted)
                {
                    HitNote(note);
                    YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with hopo rules",
                        i, note.NoteMask, CurrentTime);
                    break;
                }

                // If hopo/tap checks failed then the note can be hit if it was strummed
                if ((HasStrummed || StrumLeniencyTimer.IsActive) &&
                    (isFirstNoteInWindow || (NoteIndex > 0 && EngineStats.Combo == 0)))
                {
                    HitNote(note);
                    if (HasStrummed)
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with strum input",
                            i, note.NoteMask, CurrentTime);
                    }
                    else
                    {
                        YargLogger.LogFormatTrace("Hit note (Index: {0}, Mask: {1}) at {2} with strum leniency",
                            i, note.NoteMask, CurrentTime);
                    }

                    break;
                }
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            byte buttonsMasked = ButtonMask;
            if (ActiveSustains.Count > 0)
            {
                foreach (var sustain in ActiveSustains)
                {
                    var sustainNote = sustain.Note;

                    if (sustainNote.IsExtendedSustain)
                    {
                        // Remove the note mask if its an extended sustain
                        // Difference between NoteMask and DisjointMask is that DisjointMask is only a single fret
                        // while NoteMask is the entire chord

                        // TODO Notes cannot be hit if a sustain of the same fret is being held e.g H-ELL Solo 3C5

                        //byte sameFretsHeld = (byte) ((byte) (sustain.Note.NoteMask & note.NoteMask) & ButtonMask);

                        var maskToRemove = sustainNote.IsDisjoint ? sustainNote.DisjointMask : sustainNote.NoteMask;
                        buttonsMasked &= unchecked((byte) ~maskToRemove);
                        //buttonsMasked |= sameFretsHeld;
                    }
                }

                // If the resulting masked buttons are 0, we need to apply the Open Mask so open notes can be hit
                // Need to make a copy of the button mask to prevent modifying the original
                byte buttonMaskCopy = ButtonMask;
                if (buttonsMasked == 0)
                {
                    buttonsMasked |= OPEN_MASK;
                    buttonMaskCopy |= OPEN_MASK;
                }

                // We dont want to use masked buttons for hit logic if the buttons are identical
                if (buttonsMasked != buttonMaskCopy && IsNoteHittable(note, buttonsMasked))
                {
                    return true;
                }
            }

            // If masked/extended sustain logic didn't work, try original ButtonMask
            return IsNoteHittable(note, ButtonMask);

            static bool IsNoteHittable(GuitarNote note, byte buttonsMasked)
            {
                // Only used for sustain logic
                bool useDisjointSustainMask = note is { IsDisjoint: true, WasHit: true };

                // Use the DisjointMask for comparison if disjointed and was hit (for sustain logic)
                int noteMask = useDisjointSustainMask ? note.DisjointMask : note.NoteMask;

                // If disjointed and is sustain logic (was hit), can hit if disjoint mask matches
                if (useDisjointSustainMask && (note.DisjointMask & buttonsMasked) != 0)
                {
                    if ((note.DisjointMask & buttonsMasked) != 0)
                    {
                        return true;
                    }

                    if ((note.NoteMask & OPEN_MASK) != 0)
                    {
                        return true;
                    }
                }

                // Open chords
                // Contains open fret but the note mask is not strictly the open mask
                if ((noteMask & OPEN_MASK) != 0 && noteMask != OPEN_MASK)
                {
                    // Open chords are basically normal chords except no anchoring in any circumstances
                    // Prevents HOPO/Tap chords from being anchored

                    var buttonsMaskedWithOpen = buttonsMasked | OPEN_MASK;

                    if (buttonsMaskedWithOpen == noteMask)
                    {
                        return true;
                    }
                }

                // If holding exact note mask, can hit
                if (buttonsMasked == noteMask)
                {
                    return true;
                }

                // Anchoring

                // XORing the two masks will give the anchor (held frets) around the note.
                int anchorButtons = buttonsMasked ^ noteMask;

                // Chord logic
                if (note.IsChord)
                {
                    if (note.IsStrum)
                    {
                        // Buttons must match note mask exactly for strum chords
                        return buttonsMasked == noteMask;
                    }

                    // Anchoring hopo/tap chords

                    // Gets the lowest fret of the chord.
                    var chordMask = 0;
                    for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
                    {
                        chordMask = 1 << (int) fret;

                        // If the current fret mask is part of the chord, break
                        if ((chordMask & note.NoteMask) == chordMask)
                        {
                            break;
                        }
                    }

                    // Anchor part:
                    // Lowest fret of chord must be bigger or equal to anchor buttons
                    // (can't hold note higher than the highest fret of chord)

                    // Button mask subtract the anchor must equal chord mask (all frets of chord held)
                    return chordMask >= anchorButtons && buttonsMasked - anchorButtons == note.NoteMask;
                }

                // Anchoring single notes
                // Anchors are buttons held lower than the note mask

                // Remove the open mask from note otherwise this will always pass (as its higher than all notes)
                // This is only used for single notes, open chords are handled above
                return anchorButtons < (noteMask & unchecked((byte) ~OPEN_MASK));
            }
        }

        protected override void HitNote(GuitarNote note)
        {
            if (note.IsHopo || note.IsTap)
            {
                HasTapped = false;
                StartTimer(ref HopoLeniencyTimer, CurrentTime);
            }
            else
            {
                // This line allows for hopos/taps to be hit using infinite front end after strumming
                HasTapped = true;

                // Does the same thing but ensures it still works when infinite front end is disabled
                EngineTimer.Reset(ref FrontEndExpireTime);
            }

            StrumLeniencyTimer.Disable();

            for(int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustainNote = ActiveSustains[i].Note;

                var sustainMask = sustainNote.IsDisjoint ? sustainNote.DisjointMask : sustainNote.NoteMask;
                if ((sustainMask & note.NoteMask) != 0)
                {
                    EndSustain(i, true, CurrentTick >= sustainNote.TickEnd);
                }
            }

            base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            HasTapped = false;
            base.MissNote(note);
        }

        protected void UpdateTimers()
        {
            if (HopoLeniencyTimer.IsActive && HopoLeniencyTimer.IsExpired(CurrentTime))
            {
                HopoLeniencyTimer.Disable();

                ReRunHitLogic = true;
            }

            if (StrumLeniencyTimer.IsActive)
            {
                //YargTrace.LogInfo("Strum Leniency: Enabled");
                if (StrumLeniencyTimer.IsExpired(CurrentTime))
                {
                    //YargTrace.LogInfo("Strum Leniency: Expired. Overstrumming");
                    Overstrum();
                    StrumLeniencyTimer.Disable();

                    ReRunHitLogic = true;
                }
            }
        }

        protected bool CheckForGhostInput(GuitarNote note)
        {
            // First note cannot be ghosted, nor can a note be ghosted if a button is unpressed (pulloff)
            if (note.PreviousNote is null || !IsFretPress)
            {
                return false;
            }

            // Note can only be ghosted if it's in timing window
            if (!IsNoteInWindow(note))
            {
                return false;
            }

            // Input is a hammer-on if the highest fret held is higher than the highest fret of the previous mask
            bool isHammerOn = GetMostSignificantBit(ButtonMask) > GetMostSignificantBit(LastButtonMask);

            // Input is a hammer-on and the button pressed is not part of the note mask (incorrect fret)
            if (isHammerOn && (ButtonMask & note.NoteMask) == 0)
            {
                return true;
            }

            return false;
        }

        private static int GetMostSignificantBit(int mask)
        {
            // Gets the most significant bit of the mask
            var msbIndex = 0;
            while (mask != 0)
            {
                mask >>= 1;
                msbIndex++;
            }

            return msbIndex;
        }
    }
}
