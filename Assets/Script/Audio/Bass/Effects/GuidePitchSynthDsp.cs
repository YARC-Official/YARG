using System;
using System.Threading;
using ManagedBass;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Attaches a real-time sine wave generator to the BASS output mixer.
    ///
    /// <para>
    /// The DSP callback runs on the BASS audio thread and calls <see cref="_getSongPosition"/>
    /// to determine the song time of the buffer currently being rendered.  Because this is
    /// the render-ahead position (not the audible position) the generated tone is placed in
    /// the same time window as the mixed stems in that buffer, giving sample-accurate sync
    /// with no additional latency offset.
    /// </para>
    ///
    /// <para>
    /// The active <see cref="VocalsPart"/> (or <c>null</c> for silence) is written by the
    /// game thread via <see cref="SetPart"/> and read on the audio thread through
    /// <see langword="volatile"/> semantics.  The note chart data itself is read-only after
    /// chart load, so concurrent access is safe without a lock.
    /// </para>
    /// </summary>
    public sealed class GuidePitchSynthDsp : IDisposable
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

#nullable enable
        public static GuidePitchSynthDsp? Create(int outputMixerHandle, Func<double> getSongPosition)
#nullable disable
        {
            var info = Bass.ChannelGetInfo(outputMixerHandle);
            if (info.Frequency <= 0 || info.Channels <= 0)
            {
                YargLogger.LogFormatError("GuidePitchSynthDsp: failed to query output mixer info: {0}",
                    Bass.LastError);
                return null;
            }

            var dsp = new GuidePitchSynthDsp(outputMixerHandle, info.Frequency, info.Channels,
                getSongPosition);
            dsp._dspHandle = Bass.ChannelSetDSP(outputMixerHandle, dsp._callback, IntPtr.Zero, 0);
            if (dsp._dspHandle == 0)
            {
                YargLogger.LogFormatError("GuidePitchSynthDsp: failed to attach DSP: {0}", Bass.LastError);
                return null;
            }

            return dsp;
        }

        // ── Fields ───────────────────────────────────────────────────────────────

        private readonly int          _streamHandle;
        private readonly int          _sampleRate;
        private readonly int          _channelCount;
        private readonly DSPProcedure _callback;
        private readonly Func<double> _getSongPosition;
        private          int          _dspHandle;
        private          bool         _disposed;

        // Written by game thread; read by DSP (audio) thread.
        // VocalsPart is a class — reference reads/writes are atomic on all our targets.
        // The 'volatile' keyword provides the required load/store ordering.
        private volatile VocalsPart _targetPart; // null = silent

        // Incremented by the game thread each time _targetPart changes.
        // The DSP thread checks this to know when to reset its scan indices.
        private volatile int _resetGeneration;

        // ── DSP-thread-only state (never touched by the game thread) ─────────────
        private int    _lastSeenGeneration;
        private int    _phraseIndex;
        private int    _noteIndex;
        private double _phase;          // 0..1 normalised phase accumulator
        private float  _currentVolume;  // smoothly ramped

        // ── Construction ─────────────────────────────────────────────────────────

        private GuidePitchSynthDsp(int streamHandle, int sampleRate, int channelCount,
            Func<double> getSongPosition)
        {
            _streamHandle    = streamHandle;
            _sampleRate      = sampleRate;
            _channelCount    = channelCount;
            _getSongPosition = getSongPosition;
            _callback        = ProcessAudio;
        }

        // ── Public API (game thread) ──────────────────────────────────────────────

        /// <summary>
        /// Sets the vocal part whose notes should be sonified, or <c>null</c> to silence.
        /// Thread-safe: may be called from any thread.
        /// </summary>
        public void SetPart(VocalsPart part)
        {
            _targetPart = part;                         // volatile write
            Interlocked.Increment(ref _resetGeneration); // signal scan-state reset
        }

        // ── BASS audio thread ─────────────────────────────────────────────────────

        private unsafe void ProcessAudio(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            // ── Check for part change and reset scan if needed ──
            int gen = _resetGeneration; // volatile read via field access
            if (gen != _lastSeenGeneration)
            {
                _lastSeenGeneration = gen;
                _phraseIndex        = 0;
                _noteIndex          = 0;
            }

            VocalsPart part = _targetPart; // volatile read

            bool shouldSilence = part == null;
            if (shouldSilence && _currentVolume <= 0f)
            {
                _phase = 0.0;
                return;
            }

            // ── Determine frequency for this buffer ──
            float* output    = (float*) buffer;
            int    frames    = length / (sizeof(float) * _channelCount);
            
            // _getSongPosition() (now GetRenderPosition) calls Bass.ChannelGetPosition with Decode flag.
            // Because the mixer has just pulled from the source, this represents the END of the buffer.
            double songTimeEnd = _getSongPosition();
            
            // To find the start time, we need the duration of the buffer in song time.
            // BASS tempo streams handle speed internally, so we don't need to multiply by songSpeed here.
            // But we do need to convert frames to time. Actually, if we just interpolate songTime per sample,
            // we can handle note changes *within* the buffer perfectly.
            double bufferDuration = (double)frames / _sampleRate;
            double songTimeStart = songTimeEnd - bufferDuration;
            
            double songTimeStep = bufferDuration / frames;
            double currentSongTime = songTimeStart;

            for (int i = 0; i < frames; i++)
            {
                float targetFrequency = 0f;
                if (!shouldSilence)
                {
                    VocalNote note = FindActiveNote(part, currentSongTime);
                    if (note != null && !note.IsNonPitched && !note.IsPercussion)
                    {
                        targetFrequency = MidiPitchToHz((float) note.PitchAtSongTime(currentSongTime));
                    }
                }

                // Volume ramp toward target (0 when silent/no note, DEFAULT_VOLUME otherwise)
                float effectiveTarget = (shouldSilence || targetFrequency <= 0f) ? 0f : DEFAULT_VOLUME;
                if (_currentVolume < effectiveTarget)
                    _currentVolume = Math.Min(_currentVolume + RAMP_RATE, effectiveTarget);
                else if (_currentVolume > effectiveTarget)
                    _currentVolume = Math.Max(_currentVolume - RAMP_RATE, effectiveTarget);

                double phaseStep = targetFrequency > 0f ? (double) targetFrequency / _sampleRate : 0.0;
                float sample = _currentVolume * (float) Math.Sin(_phase * 2.0 * Math.PI);

                int frameBase = i * _channelCount;
                for (int ch = 0; ch < _channelCount; ch++)
                    output[frameBase + ch] = Math.Clamp(output[frameBase + ch] + sample, -1f, 1f);

                _phase += phaseStep;
                if (_phase >= 1.0) _phase -= 1.0;
                
                currentSongTime += songTimeStep;
            }
        }

        // ── Note scanning (DSP thread only) ──────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="VocalNote"/> (lyric type) active at <paramref name="songTime"/>,
        /// or <c>null</c> if we are in a gap.  Uses a forward-scan with sticky indices;
        /// detects backward seeks (section loop restarts) and resets automatically.
        /// </summary>
        private VocalNote FindActiveNote(VocalsPart part, double songTime)
        {
            var phrases = part.NotePhrases;
            if (phrases.Count == 0) return null;

            // ── Detect backward seek (section loop restart) ──
            bool backwardSeek = false;
            if (_phraseIndex < phrases.Count)
            {
                backwardSeek = phrases[_phraseIndex].PhraseParentNote.Time > songTime + BACKWARD_SEEK_THRESHOLD;
            }
            else if (phrases.Count > 0)
            {
                backwardSeek = phrases[^1].PhraseParentNote.TotalTimeEnd > songTime;
            }

            if (backwardSeek)
            {
                _phraseIndex = 0;
                _noteIndex   = 0;
            }

            // ── Scan forward to the active note ──
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

        // ── IDisposable ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_dspHandle != 0 && !Bass.ChannelRemoveDSP(_streamHandle, _dspHandle))
            {
                YargLogger.LogFormatError("GuidePitchSynthDsp: failed to remove DSP: {0}", Bass.LastError);
            }
            _dspHandle = 0;
        }

        // ── Utility ───────────────────────────────────────────────────────────────

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
