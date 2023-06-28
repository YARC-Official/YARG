using System.Runtime.InteropServices;
using ManagedBass;

/// <summary>
/// Parameters for PitchShift Effect.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public class PitchShiftWindowsParameters : IEffectParameter
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
    /// <remarks>It may be any value up to 8192 but it MUST be a power of 2.</remarks>
    public int lFFTsize;

    /// <summary>
    /// Is the STFT oversampling factor which also determines the overlap between adjacent STFT frames. Default = 8.
    /// </summary>
    /// <remarks>It should at least be 4 for moderate scaling ratios. A value of 32 is recommended for best quality (better quality = higher CPU usage).</remarks>
    public int lOsamp;

    /// <summary>
    /// A <see cref="FXChannelFlags" /> flag to define on which channels to apply the effect. Default: <see cref="FXChannelFlags.All"/>
    /// </summary>
    public FXChannelFlags lChannel = FXChannelFlags.All;

    /// <summary>
    /// Gets the <see cref="EffectType"/>.
    /// </summary>
    public EffectType FXType => EffectType.PitchShift;
}