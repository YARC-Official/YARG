namespace YARG.Settings.Customization
{
    public partial class CameraPreset : BasePreset
    {
        public static CameraPreset Default => new("Default");

        public float FieldOfView = 55f;

        public float PositionY = 2.66f;
        public float PositionZ = 1.14f;
        public float Rotation = 24.12f;

        public float FadeStart = 3f;
        public float FadeLength = 1.25f;

        public float CurveFactor = 0.5f;

        public CameraPreset(string name) : base(name)
        {
        }
    }
}