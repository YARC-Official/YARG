// using System;

namespace YARG.Audio
{
    public class AudioOptions
    {
        public const int WHAMMY_FFT_DEFAULT = 2048;
        public const int WHAMMY_OVERSAMPLE_DEFAULT = 8;

        public bool UseStarpowerFx { get; set; }
        public bool UseWhammyFx { get; set; }
        public bool IsChipmunkSpeedup { get; set; }

        /// <summary>
        /// The number of semitones to bend the pitch by. Must be at least 1;
        /// </summary>
        public float WhammyPitchShiftAmount { get; set; } = 1f;

        // Not implemented, as changing the FFT size causes BASS_FX to crash
        // /// <summary>
        // /// The size of the whammy FFT buffer. Must be a power of 2, up to 8192.
        // /// </summary>
        // /// <remarks>
        // /// Changes to this value will not be applied until the next song plays.
        // /// </remarks>
        // public int WhammyFFTSize
        // {
        //     get => (int)Math.Pow(2, _whammyFFTSize);
        //     set => _whammyFFTSize = (int)Math.Log(value, 2);
        // }
        // private int _whammyFFTSize = WHAMMY_FFT_DEFAULT;

        /// <summary>
        /// The oversampling factor of the whammy SFX. Must be at least 4.
        /// </summary>
        /// <remarks>
        /// Changes to this value will not be applied until the next song plays.
        /// </remarks>
        public int WhammyOversampleFactor { get; set; } = WHAMMY_OVERSAMPLE_DEFAULT;
    }
}