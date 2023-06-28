using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG
{
    public class GraphicsManager : MonoBehaviour
    {
        public static GraphicsManager Instance { get; private set; }

        [SerializeField]
        private VolumeProfile postProcessingProfile = null;

        private Bloom bloom = null;

        public bool BloomEnabled
        {
            set => bloom.active = value;
            get => bloom.active;
        }

        public bool LowQuality
        {
            set => QualitySettings.SetQualityLevel(value ? 0 : 1, true);
            get => QualitySettings.GetQualityLevel() == 0;
        }

        private void Awake()
        {
            Instance = this;

            if (!postProcessingProfile.TryGet(out bloom))
            {
                Debug.LogError("Could not find bloom component in the post process volume.");
            }
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR

            // For some reason, UNITY EDITOR SAVES THIS IN THE VOLUME PROFILE!!!!
            BloomEnabled = true;
            LowQuality = false;

#endif
        }
    }
}