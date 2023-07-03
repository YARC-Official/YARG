namespace YARG.Audio
{
    public class AudioOptions
    {
        public const int WHAMMY_FFT_DEFAULT = 2048;
        public const int WHAMMY_OVERSAMPLE_DEFAULT = 8;

        public bool UseStarpowerFx { get; set; }
        public bool UseWhammyFx { get; set; }
        public bool IsChipmunkSpeedup { get; set; }
    }
}