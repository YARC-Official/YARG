using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YARG.Core.Logging;

namespace YARG
{
    public enum StarPowerHighwayFxMode
    {
        On,
        Reduced,
        Off
    }

    public enum VenueAntiAliasingMethod
    {
        None,
        FXAA,
        MSAA,
        FSR3,
    }

    public class GraphicsManager : MonoSingleton<GraphicsManager>
    {
        [SerializeField]
        private VolumeProfile postProcessingProfile = null;

        private Bloom bloom = null;
        private FilmGrain filmGrain = null;

        public float VenueRenderScale = 1.0f;
        public VenueAntiAliasingMethod VenueAntiAliasing = VenueAntiAliasingMethod.None;

        public bool BloomEnabled
        {
            set => bloom.active = value;
            get => bloom.active;
        }

        public bool FilmGrainEnabled
        {
            set => filmGrain.active = value;
            get => filmGrain.active;
        }

        public bool LowQuality
        {
            set => QualitySettings.SetQualityLevel(value ? 0 : 1, true);
            get => QualitySettings.GetQualityLevel() == 0;
        }

        protected override void SingletonAwake()
        {
            if (!postProcessingProfile.TryGet(out bloom))
            {
                YargLogger.LogError("Could not find bloom component in the post process volume.");
            }
            if (!postProcessingProfile.TryGet(out filmGrain))
            {
                YargLogger.LogError("Could not find film grain component in the post process volume.");
            }
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR

            // For some reason, UNITY EDITOR SAVES THIS IN THE VOLUME PROFILE!!!!
            BloomEnabled = true;
            FilmGrainEnabled = true;
            LowQuality = false;

#endif
        }
    }
}
