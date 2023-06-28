using UnityEngine;

namespace YARG.Util
{
    [RequireComponent(typeof(Light))]
    public class FadeLight : MonoBehaviour
    {
        [SerializeField]
        private float fadeOutRate = 2f;

        private new Light light;
        private float startIntensity = 0f;

        private void Awake()
        {
            light = GetComponent<Light>();
            startIntensity = light.intensity;
            light.intensity = 0f;
        }

        private void Update()
        {
            if (light.intensity > 0f)
            {
                light.intensity -= Time.deltaTime * fadeOutRate;
            }
        }

        public void Play()
        {
            light.intensity = startIntensity;
        }
    }
}