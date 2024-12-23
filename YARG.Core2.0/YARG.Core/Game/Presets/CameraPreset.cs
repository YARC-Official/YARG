using YARG.Core.Game.Settings;

namespace YARG.Core.Game
{
    public partial class CameraPreset : BasePreset
    {
        [SettingType(SettingType.Slider)]
        [SettingRange(40f, 150f)]
        public float FieldOfView = 55f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 4f)]
        public float PositionY = 2.66f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 12f)]
        public float PositionZ = 1.14f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 180f)]
        public float Rotation  = 24.12f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 5f)]
        public float FadeLength = 1.25f;

        [SettingType(SettingType.Slider)]
        [SettingRange(-3f, 3f)]
        public float CurveFactor = 0.5f;

        public CameraPreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new CameraPreset(name)
            {
                FieldOfView = FieldOfView,
                PositionY = PositionY,
                PositionZ = PositionZ,
                Rotation = Rotation,
                FadeLength = FadeLength,
                CurveFactor = CurveFactor,
            };
        }
    }
}