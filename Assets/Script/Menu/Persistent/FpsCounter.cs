using System;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace YARG.Menu.Persistent
{
    public class FpsCounter : MonoSingleton<FpsCounter>
    {
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
        private int screenRate;

        protected override void SingletonAwake()
        {
            screenRate = Screen.currentResolution.refreshRate;
        }

        public void SetVisible(bool value)
        {
            fpsText.gameObject.SetActive(value);
            fpsCircle.gameObject.SetActive(value);
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
            static string MemoryUsage(long bytes)
            {
                const float unitFactor = 1024f;
                const float unitThreshold = 1.1f * unitFactor;

                // Bytes
                if (bytes < unitThreshold)
                    return $"{bytes:N0} B";

                // Kilobytes
                float kilobytes = bytes / unitFactor;
                if (kilobytes < unitThreshold)
                    return $"{kilobytes:N0} KB";

                // Megabytes
                float megaBytes = kilobytes / unitFactor;
                if (megaBytes < unitThreshold)
                    return $"{megaBytes:N1} MB";

                // Gigabytes
                float gigaBytes = megaBytes / unitFactor;
                return $"{gigaBytes:N2} GB";
            }

            // Get memory usage
            long managedMemory = GC.GetTotalMemory(false);
            long nativeMemory = Profiler.GetTotalAllocatedMemoryLong();
            long totalMemory = managedMemory + nativeMemory;

            // Display the memory usage
            fpsText.text += $"   •   <b>Memory:</b> {MemoryUsage(totalMemory)}  (managed: {MemoryUsage(managedMemory)}, native: {MemoryUsage(nativeMemory)})";
#endif

            // reset the update time
            nextUpdateTime = Time.unscaledTime + fpsUpdateRate;
        }
    }
}