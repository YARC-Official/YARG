using System.Collections.Generic;

namespace YARG.Settings.Customization
{
    public partial class CameraPreset
    {
        public static readonly List<CameraPreset> Defaults = new()
        {
            Default,
            new CameraPreset("High FOV", true)
            {
                FieldOfView = 60f,
                PositionY   = 2.66f,
                PositionZ   = 1.27f,
                Rotation    = 24.12f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("The Band 1", true)
            {
                FieldOfView = 47.84f,
                PositionY   = 2.43f,
                PositionZ   = 1.42f,
                Rotation    = 26f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("The Band 2", true)
            {
                FieldOfView = 44.97f,
                PositionY   = 2.66f,
                PositionZ   = 0.86f,
                Rotation    = 24.12f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("The Band 3", true)
            {
                FieldOfView = 57.29f,
                PositionY   = 2.22f,
                PositionZ   = 1.61f,
                Rotation    = 23.65f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("The Band 4", true)
            {
                FieldOfView = 62.16f,
                PositionY   = 2.56f,
                PositionZ   = 1.20f,
                Rotation    = 19.43f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("Hero 2", true)
            {
                FieldOfView = 58.15f,
                PositionY   = 1.82f,
                PositionZ   = 1.50f,
                Rotation    = 12.40f,
                FadeStart   = 3f,
                FadeLength  = 1.5f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("Hero 3", true)
            {
                FieldOfView = 52.71f,
                PositionY   = 2.17f,
                PositionZ   = 1.14f,
                Rotation    = 15.21f,
                FadeStart   = 3f,
                FadeLength  = 1.5f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("Hero Traveling the World", true)
            {
                FieldOfView  = 53.85f,
                PositionY    = 1.97f,
                PositionZ    = 1.52f,
                Rotation     = 16.62f,
                FadeStart    = 3f,
                FadeLength   = 1.5f,
                CurveFactor  = 0.5f,
            },
            new CameraPreset("Hero Live", true)
            {
                FieldOfView = 62.16f,
                PositionY   = 2.40f,
                PositionZ   = 1.42f,
                Rotation    = 21.31f,
                FadeStart   = 3f,
                FadeLength  = 1.25f,
                CurveFactor = 0.5f,
            },
            new CameraPreset("Clone", true)
            {
                FieldOfView = 55f,
                PositionY   = 2.07f,
                PositionZ   = 1.51f,
                Rotation    = 17.09f,
                FadeStart   = 3f,
                FadeLength  = 1.5f,
                CurveFactor = 0.5f,
            },
        };
    }
}