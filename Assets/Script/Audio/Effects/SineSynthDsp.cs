using System;
using YARG.Core.Audio;

namespace YARG.Audio.Effects
{
    /// <summary>
    /// A mixer DSP processor that mixes a sine wave into the output at the frequency
    /// reported by an <see cref="IPitchSource"/>. Implements <see cref="IMixerDspProcessor"/>
    /// so it can be attached to any <see cref="StemMixer"/> without knowledge of the
    /// audio backend, and knows nothing about where the pitch comes from.
    /// </summary>
    public sealed class SineSynthDsp : IMixerDspProcessor
    {
        /// <summary>Volume of the emitted tone relative to the master mix.</summary>
        public const float DEFAULT_VOLUME = 0.35f;

        /// <summary>
        /// Target duration for a full volume fade (0 to 1) in seconds. Long enough to
        /// avoid an audible click at note on/off boundaries, short enough to stay tight
        /// against the note onset.
        /// </summary>
        public const float DEFAULT_FADE_DURATION_SECONDS = 0.015f;

        private readonly IPitchSource _pitchSource;
        private readonly float        _volume;
        private readonly float        _fadeDurationSeconds;

        // Audio-thread-only state
        private double _phase;
        private float  _currentVolume;

        /// <param name="pitchSource">Supplies the frequency to emit at each sample.</param>
        /// <param name="volume">Volume of the emitted tone relative to the master mix.</param>
        /// <param name="fadeDurationSeconds">Declick fade duration for a full volume ramp.</param>
        public SineSynthDsp(IPitchSource pitchSource, float volume = DEFAULT_VOLUME,
            float fadeDurationSeconds = DEFAULT_FADE_DURATION_SECONDS)
        {
            _pitchSource         = pitchSource ?? throw new ArgumentNullException(nameof(pitchSource));
            _volume              = volume;
            _fadeDurationSeconds = fadeDurationSeconds;
        }

        public void ProcessAudio(Span<float> buffer, int frames, int channels, int sampleRate,
            double songTimeStart, double songTimeEnd)
        {
            float rampRate = 1f / (_fadeDurationSeconds * sampleRate);

            double songTimeStep    = (songTimeEnd - songTimeStart) / frames;
            double currentSongTime = songTimeStart;

            for (int i = 0; i < frames; i++)
            {
                float frequency = _pitchSource.GetFrequency(currentSongTime);
                currentSongTime += songTimeStep;

                float targetVolume = frequency > 0f ? _volume : 0f;
                if (_currentVolume < targetVolume)
                {
                    _currentVolume = Math.Min(_currentVolume + rampRate, targetVolume);
                }
                else if (_currentVolume > targetVolume)
                {
                    _currentVolume = Math.Max(_currentVolume - rampRate, targetVolume);
                }

                // Silent, so there is nothing to mix in. Restart the next tone from zero
                // phase so that it always begins on a zero crossing.
                if (_currentVolume <= 0f)
                {
                    _phase = 0.0;
                    continue;
                }

                float sample = _currentVolume * (float) Math.Sin(_phase * 2.0 * Math.PI);

                int frameBase = i * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    buffer[frameBase + ch] += sample;
                }

                // A frequency of 0 holds the phase, so that the fade out at the end of a
                // tone ramps down from where the waveform stopped instead of continuing it.
                if (frequency > 0f)
                {
                    _phase += (double) frequency / sampleRate;
                    if (_phase >= 1.0)
                    {
                        _phase -= 1.0;
                    }
                }
            }
        }
    }
}
