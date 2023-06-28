using UnityEngine;

namespace YARG.Util
{
    [RequireComponent(typeof(Light))]
    public class WaveyLight : MonoBehaviour
    {
        private new Light light;
        private float startIntensity = 0f;

        private void Awake()
        {
            light = GetComponent<Light>();
            startIntensity = light.intensity;
        }

        private void Update()
        {
            light.intensity = startIntensity +
                Mathf.Sin(Time.time * 30f) * 0.075f +
                Mathf.Sin(Time.time * 40f) * 0.075f;
        }
    }
}