using System;
using System.Runtime.CompilerServices;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Allocation-free implementation of the Schroeder/Freeverb reverb topology.
    /// All state is owned by the audio thread except when the processor is constructed.
    /// </summary>
    internal sealed class FreeverbProcessor
    {
        private const int REFERENCE_SAMPLE_RATE = 44100;
        private const int STEREO_SPREAD = 23;

        private const float FIXED_GAIN = 0.015f;
        private const float SCALE_DAMP = 0.4f;
        private const float SCALE_ROOM = 0.28f;
        private const float OFFSET_ROOM = 0.7f;

        private static readonly int[] COMB_TUNINGS =
        {
            1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617,
        };

        private static readonly int[] ALL_PASS_TUNINGS =
        {
            556, 441, 341, 225,
        };

        private sealed class ChannelState
        {
            public readonly CombFilter[] CombFilters;
            public readonly AllPassFilter[] AllPassFilters;

            public ChannelState(int sampleRate, int stereoOffset)
            {
                CombFilters = new CombFilter[COMB_TUNINGS.Length];
                for (int i = 0; i < CombFilters.Length; i++)
                {
                    CombFilters[i] = new CombFilter(ScaleDelay(
                        COMB_TUNINGS[i] + stereoOffset, sampleRate));
                }

                AllPassFilters = new AllPassFilter[ALL_PASS_TUNINGS.Length];
                for (int i = 0; i < AllPassFilters.Length; i++)
                {
                    AllPassFilters[i] = new AllPassFilter(ScaleDelay(
                        ALL_PASS_TUNINGS[i] + stereoOffset, sampleRate));
                }
            }

            public void Reset()
            {
                foreach (var filter in CombFilters)
                {
                    filter.Reset();
                }
                foreach (var filter in AllPassFilters)
                {
                    filter.Reset();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Process(float input, float feedback, float damp)
            {
                float output = 0;
                for (int i = 0; i < CombFilters.Length; i++)
                {
                    output += CombFilters[i].Process(input, feedback, damp);
                }
                for (int i = 0; i < AllPassFilters.Length; i++)
                {
                    output = AllPassFilters[i].Process(output);
                }
                return output;
            }
        }

        private sealed class CombFilter
        {
            private readonly float[] _buffer;
            private int _index;
            private float _filterStore;

            public CombFilter(int length)
            {
                _buffer = new float[length];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Process(float input, float feedback, float damp)
            {
                float output = Undenormalize(_buffer[_index]);
                _filterStore = Undenormalize(output * (1f - damp) + _filterStore * damp);
                _buffer[_index] = input + _filterStore * feedback;

                if (++_index == _buffer.Length)
                {
                    _index = 0;
                }
                return output;
            }

            public void Reset()
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _index = 0;
                _filterStore = 0;
            }
        }

        private sealed class AllPassFilter
        {
            private const float FEEDBACK = 0.5f;

            private readonly float[] _buffer;
            private int _index;

            public AllPassFilter(int length)
            {
                _buffer = new float[length];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Process(float input)
            {
                float buffered = Undenormalize(_buffer[_index]);
                float output = buffered - input;
                _buffer[_index] = input + buffered * FEEDBACK;

                if (++_index == _buffer.Length)
                {
                    _index = 0;
                }
                return output;
            }

            public void Reset()
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _index = 0;
            }
        }

        private readonly ChannelState[] _channels;
        private readonly int _channelCount;
        private readonly float _feedback;
        private readonly float _damp;
        private readonly float _wet;
        private readonly float _wetSame;
        private readonly float _wetCross;
        private readonly float _dry;

        public FreeverbProcessor(int sampleRate, int channelCount, float dryMix, float wetMix,
            float roomSize, float damp, float width)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }
            if (channelCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(channelCount));
            }

            _channelCount = channelCount;
            // BASS_FX exposes Freeverb's internal mix ranges directly: dry [0, 1], wet [0, 3].
            // Reference Freeverb's public setters scale normalized values by 2 and 3, but doing
            // that here would not match BASS_BFX_FREEVERB parameter semantics.
            _dry = Math.Max(0, Math.Min(1, dryMix));
            _wet = Math.Max(0, Math.Min(3, wetMix));
            _feedback = Clamp01(roomSize) * SCALE_ROOM + OFFSET_ROOM;
            _damp = Clamp01(damp) * SCALE_DAMP;
            float stereoWidth = Clamp01(width);
            _wetSame = _wet * (stereoWidth * 0.5f + 0.5f);
            _wetCross = _wet * ((1f - stereoWidth) * 0.5f);

            _channels = new ChannelState[channelCount];
            for (int channel = 0; channel < channelCount; channel++)
            {
                // Reference Freeverb offsets the right side of each stereo pair by 23 samples.
                int stereoOffset = (channel & 1) == 0 ? 0 : STEREO_SPREAD;
                _channels[channel] = new ChannelState(sampleRate, stereoOffset);
            }
        }

        public void Reset()
        {
            foreach (var channel in _channels)
            {
                channel.Reset();
            }
        }

        public unsafe void Process(float* samples, int sampleCount)
        {
            int frameCount = sampleCount / _channelCount;
            for (int frame = 0; frame < frameCount; frame++)
            {
                int frameOffset = frame * _channelCount;

                // Feed each stereo pair with its mono sum, as reference Freeverb does. For an
                // unpaired channel (including a mono mic), feed that channel directly.
                for (int channel = 0; channel < _channelCount; channel += 2)
                {
                    int rightChannel = channel + 1;
                    bool hasRightChannel = rightChannel < _channelCount;
                    float leftInput = samples[frameOffset + channel];
                    float rightInput = hasRightChannel ? samples[frameOffset + rightChannel] : leftInput;
                    float input = (hasRightChannel ? leftInput + rightInput : leftInput) * FIXED_GAIN;

                    float leftWet = _channels[channel].Process(input, _feedback, _damp);
                    float leftOutput;
                    if (hasRightChannel)
                    {
                        float rightWet = _channels[rightChannel].Process(input, _feedback, _damp);
                        leftOutput = leftWet * _wetSame + rightWet * _wetCross;
                        float rightOutput = rightWet * _wetSame + leftWet * _wetCross;

                        samples[frameOffset + rightChannel] =
                            rightOutput + rightInput * _dry;
                    }
                    else
                    {
                        leftOutput = leftWet * _wet;
                    }

                    samples[frameOffset + channel] = leftOutput + leftInput * _dry;
                }
            }
        }

        private static int ScaleDelay(int referenceLength, int sampleRate)
        {
            return Math.Max(1, (int) Math.Round(
                referenceLength * (double) sampleRate / REFERENCE_SAMPLE_RATE));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Undenormalize(float value)
        {
            return Math.Abs(value) < 1e-30f ? 0 : value;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

    }
}
