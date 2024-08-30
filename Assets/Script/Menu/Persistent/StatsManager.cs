using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using YARG.Player;
using YARG.Settings;

namespace YARG.Menu.Persistent
{
    public class StatsManager : MonoSingleton<StatsManager>
    {
        private const float BATTERY_CRITICAL_LOW_THRESHOLD = 10;
        private const float BATTERY_LOW_MEDIUM_THRESHOLD = 40;
        private const float BATTERY_MEDIUM_FULL_THRESHOLD = 90;

        public enum Stat
        {
            FPS,
            Memory,
            Time,
            Battery,
            ActivePlayers,
            ActiveBots
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
        private GameObject _time;
        [SerializeField]
        private TextMeshProUGUI _timeText;

        [Space]
        [SerializeField]
        private GameObject _battery;
        [SerializeField]
        private Image _batteryIcon;
        [SerializeField]
        private TextMeshProUGUI _batteryText;

        [Space]
        [SerializeField]
        private Sprite _batterySpriteFull;
        [SerializeField]
        private Sprite _batterySpriteMedium;
        [SerializeField]
        private Sprite _batterySpriteLow;
        [SerializeField]
        private Sprite _batterySpriteCritical;

        [Space]
        [SerializeField]
        private Color _green;
        [SerializeField]
        private Color _yellow;
        [SerializeField]
        private Color _red;

        [Space]
        [SerializeField]
        private ActivePlayerList _activePlayerList;

        [Space]
        [SerializeField]
        private GameObject _activeBots;
        [SerializeField]
        private TextMeshProUGUI _activeBotsText;

        [Space]
        [SerializeField]
        private float _updateRate;

        private float _screenRefreshRate;

        private List<float> _frameTimes = new();

        private float _nextUpdateTime;

        protected override void SingletonAwake()
        {
            _screenRefreshRate = Screen.currentResolution.refreshRate;
        }

        private GameObject GetStat(Stat stat)
        {
            return stat switch
            {
                Stat.FPS            => _fpsCounter,
                Stat.Memory         => _memoryStats,
                Stat.Time           => _time,
                Stat.Battery        => _battery,
                Stat.ActivePlayers  => _activePlayerList.gameObject,
                Stat.ActiveBots     => _activeBots,
                _                   => throw new Exception("Unreachable.")
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
            UpdateTime();
            UpdateBattery();

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
            if (fps < (_screenRefreshRate * 0.5f))
            {
                _fpsCircle.color = _red;
            }
            else if (fps < (_screenRefreshRate * 0.9f))
            {
                _fpsCircle.color = _yellow;
            }
            else
            {
                _fpsCircle.color = _green;
            }

            // Display the FPS
            _fpsText.SetTextFormat("<b>FPS:</b> {0:N1}", fps);
        }

        private void UpdateMemoryStats()
        {
            if (!IsShowing(Stat.Memory)) return;

            // Get memory usage
            long managedMemory = GC.GetTotalMemory(false);
            long nativeMemory = Profiler.GetTotalAllocatedMemoryLong();
            long totalMemory = managedMemory + nativeMemory;

            var (totalUsage, totalSuffix) = GetMemoryUsage(totalMemory);
            var (managedUsage, managedSuffix) = GetMemoryUsage(managedMemory);
            var (nativeUsage, nativeSuffix) = GetMemoryUsage(nativeMemory);

            // Display usage
            _memoryText.SetTextFormat("<b>Memory:</b> {0:0.00} {1} (managed: {2:0.00} {3}, native: {4:0.00} {5})",
                totalUsage, totalSuffix, managedUsage, managedSuffix, nativeUsage, nativeSuffix);
        }

        private static (float usage, string suffix) GetMemoryUsage(long bytes)
        {
            const float UNIT_FACTOR = 1024f;
            const float UNIT_THRESHOLD = 1.1f * UNIT_FACTOR;

            // Bytes
            if (bytes < UNIT_THRESHOLD)
                return (bytes, "B");

            // Kilobytes
            float kilobytes = bytes / UNIT_FACTOR;
            if (kilobytes < UNIT_THRESHOLD)
                return (kilobytes, "KB");

            // Megabytes
            float megaBytes = kilobytes / UNIT_FACTOR;
            if (megaBytes < UNIT_THRESHOLD)
                return (megaBytes, "MB");

            // Gigabytes
            float gigaBytes = megaBytes / UNIT_FACTOR;
            return (gigaBytes, "GB");
        }

        private void UpdateTime()
        {
            // Use current culture's short time format
            _timeText.SetTextFormat("{0:t}", DateTime.Now);
        }

        private void UpdateBattery()
        {
            if (!IsShowing(Stat.Battery)) return;

            // Show battery percentage.
            var battery = SystemInfo.batteryLevel * 100;
            _batteryText.SetTextFormat("{0:F0}%", battery);

            // Set battery icon.
            _batteryIcon.sprite = battery switch
            {
                >= BATTERY_MEDIUM_FULL_THRESHOLD => _batterySpriteFull,
                >= BATTERY_LOW_MEDIUM_THRESHOLD => _batterySpriteMedium,
                >= BATTERY_CRITICAL_LOW_THRESHOLD => _batterySpriteLow,
                _ => _batterySpriteCritical
            };
        }

        public void UpdateActivePlayers()
        {
            var activeBotCount = PlayerContainer.Players.Count(p => p.Profile.IsBot);

            // Only show the bot count if there are active bots.
            var showBots = SettingsManager.Settings.ShowActiveBots.Value && activeBotCount > 0;
            SetShowing(Stat.ActiveBots, showBots);

            _activePlayerList.UpdatePlayerList();
            _activeBotsText.text = ZString.Format("x{0}", activeBotCount);
        }
    }
}