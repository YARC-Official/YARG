using System;
using System.Threading;
using YARG.Core.Audio;
using YARG.Core.Chart;

namespace YARG.Audio.Effects
{
    /// <summary>
    /// A mixer DSP processor that generates a sine wave at the pitch of the currently
    /// active vocal note. Implements <see cref="IMixerDspProcessor"/> so it can be
    /// attached to any <see cref="StemMixer"/> without knowledge of the audio backend.
    /// </summary>
    public sealed class GuidePitchSynthDsp : IMixerDspProcessor
    {
        /// <summary>Volume ramp rate per sample — ~1.4 ms fade at 44 100 Hz.</summary>
        private const float RAMP_RATE = 1f / 64f;

        /// <summary>Volume of the guide pitch relative to the master mix.</summary>
        public const float DEFAULT_VOLUME = 0.35f;

        /// <summary>
        /// If the current scan position is this many seconds ahead of <c>songTime</c>,
        /// treat it as a backward seek (section loop) and reset the scan from the start.
        /// </summary>
        private const double BACKWARD_SEEK_THRESHOLD = 0.5;

        /// <summary>
        /// Stores the cross-thread state. Should be updated atomically.
        /// </summary>
        private class PartState
        {
            public readonly VocalsPart Part;
            public readonly int Generation;

            public PartState(VocalsPart part, int generation)
            {
                Part = part;
                Generation = generation;
            }
        }

        // Written by game thread; read by DSP (audio) thread.
        private volatile PartState _targetState = new(null, 0);

        // DSP-thread-only state
        private int    _lastSeenGeneration;
        private int    _phraseIndex;
        private int    _noteIndex;
        private double _phase;
        private float  _currentVolume;

        /// <summary>
        /// Sets the vocal part whose notes should be sonified, or <c>null</c> to silence.
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

        // IMixerDspProcessor implementation
        public unsafe void ProcessAudio(float* buffer, int frames, int channels, int sampleRate, double songTimeEnd)
        {
            PartState state = _targetState;
            if (state.Generation != _lastSeenGeneration)
            {
                _lastSeenGeneration = state.Generation;
                _phraseIndex        = 0;
                _noteIndex          = 0;
            }

            VocalsPart part = state.Part;
            bool shouldSilence = part == null;
            if (shouldSilence && _currentVolume <= 0f)
            {
                _phase = 0.0;
                return;
            }

            double bufferDuration = (double)frames / sampleRate;
            double songTimeStart = songTimeEnd - bufferDuration;
            double songTimeStep = bufferDuration / frames;
            double currentSongTime = songTimeStart;

            for (int i = 0; i < frames; i++)
            {
                float targetFrequency = 0f;
                if (!shouldSilence)
                {
                    VocalNote note = FindActiveNote(part, currentSongTime);
                    if (note is { IsNonPitched: false, IsPercussion: false })
                    {
                        targetFrequency = MidiPitchToHz(note.PitchAtSongTime(currentSongTime));
                    }
                }

                float effectiveTarget = (shouldSilence || targetFrequency <= 0f) ? 0f : DEFAULT_VOLUME;
                if (_currentVolume < effectiveTarget)
                    _currentVolume = Math.Min(_currentVolume + RAMP_RATE, effectiveTarget);
                else if (_currentVolume > effectiveTarget)
                    _currentVolume = Math.Max(_currentVolume - RAMP_RATE, effectiveTarget);

                if (_currentVolume <= 0f)
                {
                    _phase = 0.0;
                }

                double phaseStep = targetFrequency > 0f ? (double) targetFrequency / sampleRate : 0.0;
                float sample = _currentVolume * (float) Math.Sin(_phase * 2.0 * Math.PI);

                int frameBase = i * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    buffer[frameBase + ch] = Math.Clamp(buffer[frameBase + ch] + sample, -1f, 1f);
                }

                _phase += phaseStep;
                if (_phase >= 1.0)
                {
                    _phase -= 1.0;
                }

                currentSongTime += songTimeStep;
            }
        }

        // Note scanning (DSP thread only)

        /// <summary>
        /// Returns the <see cref="VocalNote"/> (lyric type) active at <paramref name="songTime"/>,
        /// or <c>null</c> if we are in a gap.  Uses a forward-scan with sticky indices;
        /// detects backward seeks (section loop restarts) and resets automatically.
        /// </summary>
        private VocalNote FindActiveNote(VocalsPart part, double songTime)
        {
            var phrases = part.NotePhrases;
            if (phrases.Count == 0) return null;

            // Detect backward seek (section loop restart)
            bool backwardSeek = false;
            if (_phraseIndex < phrases.Count)
            {
                backwardSeek = phrases[_phraseIndex].PhraseParentNote.Time > songTime + BACKWARD_SEEK_THRESHOLD;
            }
            else
            {
                backwardSeek = phrases[^1].PhraseParentNote.TotalTimeEnd > songTime;
            }

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
            if (midiPitch < 0f) return 0f;
            return 440f * MathF.Pow(2f, (midiPitch - 69f) / 12f);
        }
    }
}
