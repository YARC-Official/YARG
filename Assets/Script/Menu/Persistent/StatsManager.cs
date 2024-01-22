using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YARG.Menu.Persistent
{
    public class StatsManager : MonoSingleton<StatsManager>
    {
        public enum Stat
        {
            FPS,
            Memory
        }

        [SerializeField]
        private GameObject _fpsCounter;
        [SerializeField]
        private Image _fpsCircle;
        [SerializeField]
        private TextMeshProUGUI _fpsText;

        [Space]
        [SerializeField]
        private GameObject _memoryStats;
        [SerializeField]
        private TextMeshProUGUI _memoryText;

        [Space]
        [SerializeField]
        private Color _green;
        [SerializeField]
        private Color _yellow;
        [SerializeField]
        private Color _red;

        [Space]
        [SerializeField]
        private float _updateRate;

        private int _screenRefreshRate;

        private List<float> _frameTimes = new();

        private float _nextUpdateTime;

        protected override void SingletonAwake()
        {
            _screenRefreshRate = Screen.currentResolution.refreshRate;

#if UNITY_EDITOR
            SetShowing(Stat.Memory, true);
#else
            SetShowing(Stat.Memory, false);
#endif
}

        private GameObject GetStat(Stat stat)
        {
            return stat switch
            {
                Stat.FPS    => _fpsCounter,
                Stat.Memory => _memoryStats,
                _           => throw new Exception("Unreachable.")
            };
        }

        public void SetShowing(Stat stat, bool active)
        {
            GetStat(stat).SetActive(active);
        }

        public bool IsShowing(Stat stat)
        {
            return GetStat(stat).activeSelf;
        }

        private void Update()
        {
            _frameTimes.Add(Time.unscaledDeltaTime);

            // Wait for next update period
            if (Time.unscaledTime < _nextUpdateTime) return;

            UpdateFpsCounter();
            UpdateMemoryStats();

            // Reset the update time
            _nextUpdateTime = Time.unscaledTime + _updateRate;
        }

        private void UpdateFpsCounter()
        {
            if (!IsShowing(Stat.FPS)) return;

            // Get FPS
            // Averaged to smooth out brief lag frames
            float fps = 1f / _frameTimes.Average();
            _frameTimes.Clear();

            // Color the circle sprite based on the FPS
            if (fps < _screenRefreshRate / 2)
            {
                _fpsCircle.color = _red;
            }
            else if (fps < _screenRefreshRate)
            {
                _fpsCircle.color = _yellow;
            }
            else
            {
                _fpsCircle.color = _green;
            }

            // Display the FPS
            _fpsText.text = $"<b>FPS:</b> {fps:N1}";
        }

        private void UpdateMemoryStats()
        {
            if (!IsShowing(Stat.Memory)) return;

            // Get memory usage
            long managedMemory = GC.GetTotalMemory(false);
            long nativeMemory = Profiler.GetTotalAllocatedMemoryLong();
            long totalMemory = managedMemory + nativeMemory;

            // Display the memory usage
            _memoryText.text = $"<b>Memory:</b> {GetMemoryUsage(totalMemory)} " +
                $"(managed: {GetMemoryUsage(managedMemory)}, native: {GetMemoryUsage(nativeMemory)})";
        }

        private static string GetMemoryUsage(long bytes)
        {
            const float UNIT_FACTOR = 1024f;
            const float UNIT_THRESHOLD = 1.1f * UNIT_FACTOR;

            // Bytes
            if (bytes < UNIT_THRESHOLD)
                return $"{bytes:N0} B";

            // Kilobytes
            float kilobytes = bytes / UNIT_FACTOR;
            if (kilobytes < UNIT_THRESHOLD)
                return $"{kilobytes:N0} KB";

            // Megabytes
            float megaBytes = kilobytes / UNIT_FACTOR;
            if (megaBytes < UNIT_THRESHOLD)
                return $"{megaBytes:N1} MB";

            // Gigabytes
            float gigaBytes = megaBytes / UNIT_FACTOR;
            return $"{gigaBytes:N2} GB";
        }
    }
}