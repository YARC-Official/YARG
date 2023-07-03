using System;
using System.Runtime.InteropServices;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Parameters for PitchShift Effect.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PitchShiftParametersStruct : IEffectParameter
    {
        /// <summary>
        /// A factor value which is between 0.5 (one octave down) and 2 (one octave up) (1 won't change the pitch, default).
        /// </summary>
        public float fPitchShift;

        /// <summary>
        /// Semitones (0 won't change the pitch). Default = 0.
        /// </summary>
        public float fSemitones;

        /// <summary>
        /// Defines the FFT frame size used for the processing. Typical values are 1024, 2048 (default) and 4096, max is 8192.
        /// </summary>
        /// <remarks>
        /// It may be any value up to 8192 but it MUST be a power of 2.
        /// </remarks>
        public int FFTSize
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            get => lFFTsize;
            // set => lFFTsize = value;
#else
            get => (int)lFFTsize;
            // set => lFFTsize = (IntPtr)value;
#endif
        }

        // longs in C are always 32-bit on Windows/MSVC, but are a pointer size on Unix/macOS/GCC
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private int lFFTsize;
#else
        private IntPtr lFFTsize;
#endif

        /// <summary>
        /// Is the STFT oversampling factor which also determines the overlap between adjacent STFT frames. Default = 8.
        /// </summary>
        /// <remarks>
        /// It should at least be 4 for moderate scaling ratios. A value of 32 is recommended for best quality (better quality = higher CPU usage).
        /// </remarks>
        public int OversampleFactor
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            get => lOsamp;
            set => lOsamp = value;
#else
            get => (int)lOsamp;
            set => lOsamp = (IntPtr)value;
#endif
        }

        // longs in C are always 32-bit on Windows/MSVC, but are a pointer size on Unix/macOS/GCC
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private int lOsamp;
#else
        private IntPtr lOsamp;
#endif

        /// <summary>
        /// A <see cref="FXChannelFlags" /> flag to define on which channels to apply the effect. Default: <see cref="FXChannelFlags.All"/>
        /// </summary>
        private FXChannelFlags lChannel;

        public PitchShiftParametersStruct(float pitch, float semitones, int fftSize, int oversample)
        {
            fPitchShift = pitch;
            fSemitones = semitones;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            lFFTsize = fftSize;
            lOsamp = oversample;
#else
            lFFTsize = (IntPtr)fftSize;
            lOsamp = (IntPtr)oversample;
#endif
            lChannel = FXChannelFlags.All;
        }

        /// <summary>
        /// Gets the <see cref="EffectType"/>.
        /// </summary>
        public readonly EffectType FXType => EffectType.PitchShift;
    }
}