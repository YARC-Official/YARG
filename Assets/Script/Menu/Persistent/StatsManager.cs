using Cysharp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
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
            ActiveBots,
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

        private int _screenRefreshRate;

        private List<float> _frameTimes = new();

        private float _nextUpdateTime;

        private int ActiveBotCount => PlayerContainer.Players.Count(p => p.Profile.IsBot);

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
                Stat.ActiveBots     => _activeBots,
                Stat.ActivePlayers  => _activePlayerList.gameObject,
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

            // Check if battery is discharging and we get a valid level.
            var showBattery = SettingsManager.Settings.ShowBattery.Value
                && SystemInfo.batteryStatus == BatteryStatus.Discharging
                && SystemInfo.batteryLevel is >= 0 and <= 1;
            SetShowing(Stat.Battery, showBattery);

            // Only show the bot count if there are active bots.
            var showBots = SettingsManager.Settings.ShowActiveBots.Value
                && ActiveBotCount > 0;
            SetShowing(Stat.ActiveBots, showBots);

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
            _fpsText.SetTextFormat("<b>FPS:</b> {0:N1}", fps);
        }

        private void UpdateMemoryStats()
        {
            if (!IsShowing(Stat.Memory)) return;

            // Get memory usage
            long managedMemory = GC.GetTotalMemory(false);
            long nativeMemory = Profiler.GetTotalAllocatedMemoryLong();
            long totalMemory = managedMemory + nativeMemory;

            var managedUsage = GetMemoryUsage(managedMemory);
            var nativeUsage = GetMemoryUsage(nativeMemory);
            var totalUsage = GetMemoryUsage(totalMemory);

            // Display the memory usage
            using var builder = ZString.CreateStringBuilder(true);

            const string memoryFormat = "{0:0.00} {1}";

            builder.Append("<b>Memory:</b> ");
            builder.AppendFormat(memoryFormat, totalUsage.usage, totalUsage.suffix);
            builder.Append(" (managed: ");
            builder.AppendFormat(memoryFormat, managedUsage.usage, managedUsage.suffix);
            builder.Append(", native: ");
            builder.AppendFormat(memoryFormat, nativeUsage.usage, nativeUsage.suffix);
            builder.Append(")");

            _memoryText.SetText(builder);
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
            var time = DateTime.Now.ToShortTimeString();
            _timeText.SetText(time);
        }

        private void UpdateBattery()
        {
            if (!IsShowing(Stat.Battery)) return;

            // Show battery percentage.
            var battery = SystemInfo.batteryLevel * 100;
            _batteryText.SetText($"{battery:F0}%");

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
            _activePlayerList.UpdatePlayerList(PlayerContainer.Players);
            _activeBotsText.text = $"x{ActiveBotCount}";
        }
    }
}