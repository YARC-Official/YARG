using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Networking
{
    internal class DedicatedServerBootstrap : MonoBehaviour
    {
        private DedicatedServerConfig _config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var config = DedicatedServerConfig.CreateFromSources();
            if (!config.Enabled)
            {
                return;
            }

            ApplyHeadlessGraphicsSettings();

            var go = new GameObject(nameof(DedicatedServerBootstrap));
            DontDestroyOnLoad(go);
            var bootstrap = go.AddComponent<DedicatedServerBootstrap>();
            bootstrap.Configure(config);
        }

        private void Configure(DedicatedServerConfig config)
        {
            _config = config;
        }

        private void Start()
        {
            Application.runInBackground = true;
            StartCoroutine(Bootstrap());
        }

        private IEnumerator Bootstrap()
        {
            while (YargNetworkManager.Instance == null)
            {
                yield return null;
            }

            var manager = YargNetworkManager.Instance;
            manager.ConfigureDedicatedServerNetworking(_config.TransportPortOverride, _config.DiscoveryPortOverride, _config.AdvertiseDiscovery);
            manager.LaunchDedicatedServer(_config);
        }

        private static void ApplyHeadlessGraphicsSettings()
        {
            try
            {
                if (GraphicsSettings.defaultRenderPipeline != null)
                {
                    GraphicsSettings.defaultRenderPipeline = null;
                }

                if (GraphicsSettings.defaultRenderPipeline != null)
                {
                    GraphicsSettings.defaultRenderPipeline = null;
                }

                if (QualitySettings.renderPipeline != null)
                {
                    QualitySettings.renderPipeline = null;
                }

                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.vSyncCount = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DedicatedServer] Failed to adjust graphics settings for headless mode: {ex.Message}");
            }
        }
    }

    internal readonly struct DedicatedServerConfig
    {
        private DedicatedServerConfig(
            bool enabled,
            string lobbyName,
            string hostName,
            int maxPlayers,
            YargNetworkManager.LobbyPrivacyMode privacyMode,
            string password,
            bool advertiseDiscovery,
            int? transportPortOverride,
            int? discoveryPortOverride)
        {
            Enabled = enabled;
            LobbyName = lobbyName;
            HostName = hostName;
            MaxPlayers = maxPlayers;
            PrivacyMode = privacyMode;
            Password = password;
            AdvertiseDiscovery = advertiseDiscovery;
            TransportPortOverride = transportPortOverride;
            DiscoveryPortOverride = discoveryPortOverride;
        }

        public bool Enabled { get; }
        public string LobbyName { get; }
        public string HostName { get; }
        public int MaxPlayers { get; }
        public YargNetworkManager.LobbyPrivacyMode PrivacyMode { get; }
        public string Password { get; }
        public bool AdvertiseDiscovery { get; }
        public int? TransportPortOverride { get; }
        public int? DiscoveryPortOverride { get; }

        public static DedicatedServerConfig CreateFromSources()
        {
            bool enabled = CommandLineArgs.DedicatedServer || IsEnvTrue(Environment.GetEnvironmentVariable("YARG_DEDICATED") ?? string.Empty);
            if (!enabled)
            {
                return default;
            }

            string lobbyName = FirstNonEmpty(CommandLineArgs.DedicatedLobbyName ?? string.Empty, Environment.GetEnvironmentVariable("YARG_LOBBY_NAME") ?? string.Empty);
            if (string.IsNullOrWhiteSpace(lobbyName))
            {
                lobbyName = "YARG Dedicated Server";
            }

            string hostName = FirstNonEmpty(Environment.GetEnvironmentVariable("YARG_HOST_NAME") ?? string.Empty, lobbyName);
            if (string.IsNullOrWhiteSpace(hostName))
            {
                hostName = "Server";
            }

            int maxPlayers = ParseMaxPlayers(FirstNonEmpty(CommandLineArgs.DedicatedMaxPlayers ?? string.Empty, Environment.GetEnvironmentVariable("YARG_MAX_PLAYERS") ?? string.Empty));

            var privacyMode = ParsePrivacy(FirstNonEmpty(CommandLineArgs.DedicatedPrivacyMode ?? string.Empty, Environment.GetEnvironmentVariable("YARG_PRIVACY") ?? string.Empty));
            string password = FirstNonEmpty(CommandLineArgs.DedicatedPassword ?? string.Empty, Environment.GetEnvironmentVariable("YARG_PASSWORD") ?? string.Empty);

            if (privacyMode == YargNetworkManager.LobbyPrivacyMode.Private && string.IsNullOrWhiteSpace(password))
            {
                password = Guid.NewGuid().ToString("N");
                Debug.LogWarning("[DedicatedServer] Private lobby requested without password. Generated random password.");
            }

            bool discoveryEnabled = DetermineDiscoveryEnabled();
            int? transportPortOverride = ParseOptionalPort(FirstNonEmpty(CommandLineArgs.DedicatedPort ?? string.Empty,
                Environment.GetEnvironmentVariable("YARG_SERVER_PORT") ?? string.Empty,
                Environment.GetEnvironmentVariable("YARG_PORT") ?? string.Empty), "transport");
            int? discoveryPortOverride = ParseOptionalPort(FirstNonEmpty(CommandLineArgs.DedicatedDiscoveryPort ?? string.Empty,
                Environment.GetEnvironmentVariable("YARG_DISCOVERY_PORT") ?? string.Empty), "discovery");

            if (!discoveryPortOverride.HasValue && transportPortOverride.HasValue)
            {
                discoveryPortOverride = transportPortOverride;
            }

            return new DedicatedServerConfig(true, lobbyName, hostName, maxPlayers, privacyMode, password, discoveryEnabled, transportPortOverride, discoveryPortOverride);
        }

        private static bool IsEnvTrue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var entry in values)
            {
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    return entry;
                }
            }

            return string.Empty;
        }

        private static bool DetermineDiscoveryEnabled()
        {
            if (CommandLineArgs.DedicatedDisableDiscovery)
            {
                return false;
            }

            string disableEnv = Environment.GetEnvironmentVariable("YARG_DISABLE_DISCOVERY") ?? string.Empty;
            if (IsEnvTrue(disableEnv))
            {
                return false;
            }

            string discoveryToggle = Environment.GetEnvironmentVariable("YARG_DISCOVERY") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(discoveryToggle))
            {
                return IsEnvTrue(discoveryToggle);
            }

            return true;
        }

        private static int ParseMaxPlayers(string value)
        {
            int[] allowed = { 2, 4, 8 };
            const int fallback = 8;

            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (int.TryParse(value, out int parsed) && Array.IndexOf(allowed, parsed) >= 0)
            {
                return parsed;
            }

            Debug.LogWarning($"[DedicatedServer] Unsupported max player count '{value}', using {fallback} instead.");
            return fallback;
        }

        private static int? ParseOptionalPort(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            value = value.Trim();
            if (int.TryParse(value, out int parsed) && parsed > 0 && parsed <= ushort.MaxValue)
            {
                return parsed;
            }

            Debug.LogWarning($"[DedicatedServer] Ignoring invalid {label} port '{value}'. Must be 1-{ushort.MaxValue}.");
            return null;
        }

        private static YargNetworkManager.LobbyPrivacyMode ParsePrivacy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return YargNetworkManager.LobbyPrivacyMode.Public;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "public":
                    return YargNetworkManager.LobbyPrivacyMode.Public;
                case "private":
                    return YargNetworkManager.LobbyPrivacyMode.Private;
                default:
                    Debug.LogWarning($"[DedicatedServer] Unknown privacy mode '{value}', using public.");
                    return YargNetworkManager.LobbyPrivacyMode.Public;
            }
        }
    }
}
