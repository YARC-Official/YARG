namespace YARG.Audio.Effects
{
    /// <summary>
    /// Supplies the frequency a synth should emit at a given song position.
    /// </summary>
    /// <remarks>
    /// Implementations are queried from the audio thread, once per sample, and must
    /// therefore be allocation-free and non-blocking.
    /// </remarks>
    public interface IPitchSource
    {
        /// <summary>
        /// Returns the frequency (in Hz) to emit at <paramref name="songTime"/>,
        /// or 0 for silence.
        /// </summary>
        float GetFrequency(double songTime);
    }
}
