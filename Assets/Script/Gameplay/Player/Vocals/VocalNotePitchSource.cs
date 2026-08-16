using System;
using System.Threading;
using YARG.Audio.Effects;
using YARG.Core.Chart;

namespace YARG.Gameplay.Player
{
    /// <summary>
    /// An <see cref="IPitchSource"/> that reports the pitch of the vocal note active
    /// at a given song time, for a <see cref="VocalsPart"/> selected on the game thread.
    /// </summary>
    public sealed class VocalNotePitchSource : IPitchSource
    {
        /// <summary>
        /// If the queried song time jumps this many seconds behind the previously queried
        /// time, treat it as a backward seek (section loop) and reset the scan from the start.
        /// </summary>
        private const double BACKWARD_SEEK_THRESHOLD = 0.5;

        /// <summary>
        /// Stores the cross-thread state. Should be updated atomically.
        /// </summary>
        private class PartState
        {
            public readonly VocalsPart Part;
            public readonly int        Generation;

            public PartState(VocalsPart part, int generation)
            {
                Part = part;
                Generation = generation;
            }
        }

        // Written by game thread; read by the audio thread.
        private volatile PartState _targetState = new(null, 0);

        // Audio-thread-only state
        private int    _lastSeenGeneration;
        private int    _phraseIndex;
        private int    _noteIndex;
        private double _lastSongTime = double.NegativeInfinity;

        /// <summary>
        /// Sets the vocal part whose notes should be sonified, or <c>null</c> to silence.
        /// Also resets the note scan, so it must be called again whenever the notes of the
        /// current part change (such as on a practice section change).
        /// Thread-safe: may be called from any thread.
        /// </summary>
        public void SetPart(VocalsPart part)
        {
            PartState current;
            PartState next;
            do
            {
                current = _targetState;
                next = new PartState(part, current.Generation + 1);
            } while (Interlocked.CompareExchange(ref _targetState, next, current) != current);
        }

        // IPitchSource implementation (audio thread)
        public float GetFrequency(double songTime)
        {
            var state = _targetState;
            if (state.Generation != _lastSeenGeneration)
            {
                _lastSeenGeneration = state.Generation;
                _phraseIndex        = 0;
                _noteIndex          = 0;
                _lastSongTime       = double.NegativeInfinity;
            }

            var part = state.Part;
            if (part == null)
            {
                return 0f;
            }

            var note = FindActiveNote(part, songTime);
            if (note is not { IsNonPitched: false, IsPercussion: false })
            {
                return 0f;
            }

            return MidiPitchToHz(note.PitchAtSongTime(songTime));
        }

        // Note scanning (audio thread only)

        /// <summary>
        /// Returns the <see cref="VocalNote"/> (lyric type) active at <paramref name="songTime"/>,
        /// or <c>null</c> if we are in a gap.  Uses a forward-scan with sticky indices;
        /// detects backward seeks (section loop restarts) and resets automatically.
        /// </summary>
        private VocalNote FindActiveNote(VocalsPart part, double songTime)
        {
            var phrases = part.NotePhrases;
            if (phrases.Count == 0)
            {
                return null;
            }

            // Detect a backward seek (section loop restart) by comparing against the time we last
            // serviced, not against the upcoming phrase: the scan indices sit on the *next* phrase
            // while we are in a gap, so testing that phrase's start time treats every gap longer
            // than the threshold as a seek and rescans the whole part on every sample.
            bool backwardSeek = songTime < _lastSongTime - BACKWARD_SEEK_THRESHOLD;
            _lastSongTime = songTime;

            if (backwardSeek)
            {
                _phraseIndex = 0;
                _noteIndex   = 0;
            }

            // Scan forward to the active note
            while (_phraseIndex < phrases.Count)
            {
                var childNotes = phrases[_phraseIndex].PhraseParentNote.ChildNotes;

                // Advance past ended notes within the current phrase
                while (_noteIndex < childNotes.Count && childNotes[_noteIndex].TotalTimeEnd <= songTime)
                {
                    _noteIndex++;
                }

                // If we haven't exhausted all notes in this phrase, we are in the right phrase
                if (_noteIndex < childNotes.Count)
                {
                    var note = childNotes[_noteIndex];
                    // If the current note has started, it is active
                    if (note.Time <= songTime)
                    {
                        return note;
                    }

                    // Otherwise, we are in a gap before the next note in this phrase
                    return null;
                }

                // Exhausted all notes in this phrase, move to the next phrase
                _phraseIndex++;
                _noteIndex = 0;
            }

            return null;
        }

        /// <summary>
        /// Converts a MIDI pitch (float, e.g. 60 = C4) to Hz via equal temperament.
        /// Returns 0 for non-pitched values (pitch &lt; 0).
        /// </summary>
        public static float MidiPitchToHz(float midiPitch)
        {
            if (midiPitch < 0f)
            {
                return 0f;
            }

            return 440f * MathF.Pow(2f, (midiPitch - 69f) / 12f);
        }
    }
}
