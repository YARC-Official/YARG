namespace YARG.Settings.Customization
{
    public partial class CameraSettings
    {
        public static CameraSettings Default => new("Default");

        public string Name;

        public float FieldOfView = 55f;

        public float PositionY = 2.66f;
        public float PositionZ = 1.14f;
        public float Rotation = 24.12f;

        public float FadeStart = 3f;
        public float FadeLength = 1.25f;

        public CameraSettings(string name)
        {
            Name = name;
        }
    }
}