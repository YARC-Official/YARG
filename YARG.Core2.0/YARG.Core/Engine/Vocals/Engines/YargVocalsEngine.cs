using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Vocals.Engines
{
    public class YargVocalsEngine : VocalsEngine
    {
        public YargVocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack,
            VocalsEngineParameters engineParameters, bool isBot, SongChart FullChart)
            : base(chart, syncTrack, engineParameters, isBot, FullChart)
        {
        }

        protected override void UpdateBot(double songTime)
        {
            if (!IsBot)
            {
                return;
            }

            var phrase = Notes[NoteIndex];

            // Handle singing notes
            var singNote = GetNoteInPhraseAtSongTick(phrase, CurrentTick);
            if (singNote is not null)
            {
                // Bots are queued extra updates to account for in-between "inputs"
                PitchSang = singNote.PitchAtSongTime(songTime);
                HasSang = true;
                OnSing?.Invoke(true);
            }
            else
            {
                // Stop hitting to prevent the hit particles from showing up too much
                OnHit?.Invoke(false);
            }

            // Handle percussion notes
            var percussion = GetNextPercussionNote(phrase, CurrentTick);
            if (percussion is not null && songTime >= percussion.Time)
            {
                HasHit = true;
            }
        }

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            var action = gameInput.GetAction<VocalsAction>();

            if (action is VocalsAction.Hit && gameInput.Button)
            {
                HasHit = true;
            }
            else if (action is VocalsAction.Pitch)
            {
                HasSang = true;
                PitchSang = gameInput.Axis;

                OnSing?.Invoke(true);
            }
            else if (action is VocalsAction.StarPower)
            {
                IsStarPowerInputActive = gameInput.Button;
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            UpdateStarPower();

            // Quit early if there are no notes left
            if (NoteIndex >= Notes.Count)
            {
                HasSang = false;
                return;
            }

            UpdateBot(time);

            var phrase = Notes[NoteIndex];
            PhraseTicksTotal ??= GetTicksInPhrase(phrase);

            CheckForNoteHit();

            // Check for the end of a phrase
            if (CurrentTick > phrase.TickEnd)
            {
                bool hasNotes = PhraseTicksTotal.Value != 0;

                var percentHit = PhraseTicksHit / PhraseTicksTotal.Value;
                if (!hasNotes)
                {
                    percentHit = 1.0;
                }

                bool hit = percentHit >= EngineParameters.PhraseHitPercent;
                if (hit)
                {
                    EngineStats.TicksHit += PhraseTicksTotal.Value;
                    HitNote(phrase);
                }
                else
                {
                    var ticksHit = (uint) Math.Round(PhraseTicksHit);

                    EngineStats.TicksHit += ticksHit;
                    EngineStats.TicksMissed += PhraseTicksTotal.Value - ticksHit;

                    MissNote(phrase, percentHit);
                }

                PhraseTicksHit = 0;
                PhraseTicksTotal = null;

                if (hasNotes)
                {
                    OnPhraseHit?.Invoke(percentHit / EngineParameters.PhraseHitPercent, hit);
                }
            }
        }

        protected override void CheckForNoteHit()
        {
            CheckSingingHit();
            CheckPercussionHit();
        }

        private void CheckSingingHit()
        {
            if (!HasSang)
            {
                return;
            }

            HasSang = false;
            var lastSingTick = LastSingTick;
            LastSingTick = CurrentTick;

            // If the last sing detected was on the same tick (or less), skip it
            // since we've already handled that tick.
            if (lastSingTick >= CurrentTick)
            {
                return;
            }

            var phrase = Notes[NoteIndex];

            // Get the note that we should currently be targeting
            var note = GetNoteInPhraseAtSongTick(phrase, CurrentTick);
            if (note is null)
            {
                // If we're not on a note, we cannot be hitting a note
                OnHit?.Invoke(false);

                return;
            }

            OnTargetNoteChanged?.Invoke(note);

            // This will never be a percussion note here
            if (CanVocalNoteBeHit(note, out float hitPercent))
            {
                // We will apply a leniency here and assume that it will also hit all other
                // ticks, since the user cannot change pitch between inputs.
                var maxLeniency = 1.0 / EngineParameters.ApproximateVocalFps;
                var lastTick = Math.Max(
                    SyncTrack.TimeToTick(CurrentTime - maxLeniency),
                    lastSingTick);

                var ticksSinceLast = CurrentTick - lastTick;
                PhraseTicksHit += ticksSinceLast * hitPercent;

                OnHit?.Invoke(true);
            }
            else
            {
                OnHit?.Invoke(false);
            }
        }

        private void CheckPercussionHit()
        {
            var phrase = Notes[NoteIndex];
            var note = GetNextPercussionNote(phrase, CurrentTick);

            if (note is not null)
            {
                if (IsNoteInWindow(note, out var missed))
                {
                    if (HasHit)
                    {
                        HitNote(note);
                    }
                }
                else if (missed)
                {
                    // Miss out the back end
                    MissNote(note);
                }
            }
            else
            {
                // Singing (or any noise) can result in a call to CheckPercussionHit() as well, so we need to check SingToActivateStarPower here.
                if (HasHit && CanStarPowerActivate && EngineParameters.SingToActivateStarPower)
                {
                    ActivateStarPower();
                }
            }

            HasHit = false;
        }

        protected override bool CanVocalNoteBeHit(VocalNote note, out float hitPercent)
        {
            // If it is non-pitched, it is always hittable
            if (note.IsNonPitched)
            {
                hitPercent = 1f;
                return true;
            }

            var expectedPitch = note.PitchAtSongTime(CurrentTime);

            // Formula for calculating the distance to the expected pitch, while ignoring octaves
            float distanceToExpected = Math.Min(
                Mod(PitchSang - expectedPitch, 12f),
                Mod(expectedPitch - PitchSang, 12f));

            // If it is within the full points window, award full points
            if (distanceToExpected <= EngineParameters.PitchWindowPerfect)
            {
                hitPercent = 1f;
                return true;
            }

            // If it is outside of the total pitch window, then award no points
            if (distanceToExpected > EngineParameters.PitchWindow)
            {
                hitPercent = 0f;
                return false;
            }

            hitPercent = YargMath.InverseLerpF(
                EngineParameters.PitchWindow,
                EngineParameters.PitchWindowPerfect,
                distanceToExpected);
            return true;

            // Positive remainder
            static float Mod(float a, float b)
            {
                var remainder = a % b;
                if (remainder < 0)
                {
                    if (b < 0)
                    {
                        return remainder - b;
                    }

                    return remainder + b;
                }

                return remainder;
            }
        }

        protected override bool CanNoteBeHit(VocalNote note) => throw new NotImplementedException();
    }
}