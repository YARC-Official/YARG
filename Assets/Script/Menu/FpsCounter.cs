using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace YARG.UI
{
    public class FpsCounter : MonoBehaviour
    {
        public static FpsCounter Instance { get; private set; } = null;

        [SerializeField]
        private Image fpsCircle;

        [SerializeField]
        private TextMeshProUGUI fpsText;

        [SerializeField]
        private Color fpsCircleGreen;

        [SerializeField]
        private Color fpsCircleYellow;

        [SerializeField]
        private Color fpsCircleRed;

        [SerializeField]
        private float fpsUpdateRate;

        private float nextUpdateTime;
        int screenRate;

        private void Awake()
        {
            Instance = this;
            screenRate = Screen.currentResolution.refreshRate;
        }

        public void SetVisible(bool value)
        {
            fpsText.gameObject.SetActive(value);
            fpsCircle.gameObject.SetActive(value);
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        void Update()
        {
            // Wait for next update period
            if (Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            int fps = (int) (1f / Time.unscaledDeltaTime);

            // Color the circle sprite based on the FPS
            // red if lower than 30, yellow if lower than screen refresh rate, green otherwise
            if (fps < 30)
            {
                fpsCircle.color = fpsCircleRed;
            }
            else if (fps < screenRate)
            {
                fpsCircle.color = fpsCircleYellow;
            }
            else
            {
                fpsCircle.color = fpsCircleGreen;
            }

            // Display the FPS
            fpsText.text = $"<b>FPS:</b> {fps}";

#if UNITY_EDITOR
            // Display the memory usage
            fpsText.text += $"   •   <b>Memory:</b> {Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024} MB";
#endif

            // reset the update time
            nextUpdateTime = Time.unscaledTime + fpsUpdateRate;
        }
    }
}