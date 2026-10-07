using System;
using Newtonsoft.Json.Linq;
using YARG.Core.Logging;

namespace YARG.Settings
{
    internal static class SettingsMigration
    {
        internal const int CURRENT_SETTINGS_SCHEMA_VERSION = 1;

        private const string SETTINGS_SCHEMA_VERSION_PROPERTY = "SettingsSchemaVersion";
        private const string LEGACY_CROWD_FX_SETTING = "UseCrowdFx";

        private const string CROWD_CHEERING_SETTING = nameof(SettingsManager.SettingContainer.UseCrowdCheering);
        private const string CROWD_IDLE_SETTING = nameof(SettingsManager.SettingContainer.UseCrowdIdle);
        private const string STAR_POWER_CLAPS_SETTING = nameof(SettingsManager.SettingContainer.UseStarPowerClaps);
        private const string PERFORMANCE_CLAPS_SETTING = nameof(SettingsManager.SettingContainer.UsePerformanceClaps);

        private enum LegacyCrowdFxMode
        {
            Disabled = 0,
            StarpowerClapsOnly = 1,
            Enabled = 2
        }

        internal static JObject Migrate(JObject settings, out bool canSave)
        {
            canSave = true;

            if (!TryGetSettingsSchemaVersion(settings, out var version))
            {
                canSave = false;
                YargLogger.LogWarning("Settings file has an invalid schema version and will not be saved.");
                return settings;
            }

            if (version > CURRENT_SETTINGS_SCHEMA_VERSION)
            {
                canSave = false;
                YargLogger.LogFormatWarning(
                    "Settings file uses unsupported schema version {0} and will not be saved.", version);
                return settings;
            }

            while (version < CURRENT_SETTINGS_SCHEMA_VERSION)
            {
                switch (version)
                {
                    case 0:
                        MigrateSettingsV0ToV1(settings);
                        version = 1;
                        break;
                    default:
                        canSave = false;
                        YargLogger.LogFormatWarning(
                            "Settings file has no migration for schema version {0} and will not be saved.", version);
                        return settings;
                }
            }

            SetCurrentSchemaVersion(settings);
            MigrateLegacyEffectsMode(settings);
            return settings;
        }

        internal static void SetCurrentSchemaVersion(JObject settings)
        {
            settings[SETTINGS_SCHEMA_VERSION_PROPERTY] = CURRENT_SETTINGS_SCHEMA_VERSION;
        }

        private static bool TryGetSettingsSchemaVersion(JObject settings, out int version)
        {
            version = 0;

            if (!settings.TryGetValue(SETTINGS_SCHEMA_VERSION_PROPERTY, out var token))
            {
                return true;
            }

            if (!TryGetInteger(token, out var rawVersion) || rawVersion < 0 || rawVersion > int.MaxValue)
            {
                return false;
            }

            version = (int) rawVersion;
            return true;
        }

        private static void MigrateSettingsV0ToV1(JObject settings)
        {
            MigrateLegacyCrowdSettings(settings);
        }

        private static void MigrateLegacyCrowdSettings(JObject settings)
        {
            if (!TryGetLegacyCrowdFxMode(settings, out var crowdFxMode))
            {
                return;
            }

            SetIfMissing(settings, CROWD_CHEERING_SETTING, crowdFxMode == LegacyCrowdFxMode.Enabled);
            SetIfMissing(settings, CROWD_IDLE_SETTING, crowdFxMode == LegacyCrowdFxMode.Enabled);
            SetIfMissing(settings, STAR_POWER_CLAPS_SETTING, crowdFxMode != LegacyCrowdFxMode.Disabled);
            SetIfMissing(settings, PERFORMANCE_CLAPS_SETTING, crowdFxMode == LegacyCrowdFxMode.Enabled);
            settings.Remove(LEGACY_CROWD_FX_SETTING);
        }

        private static void SetIfMissing(JObject settings, string propertyName, bool value)
        {
            if (!settings.ContainsKey(propertyName))
            {
                settings[propertyName] = value;
            }
        }

        private static bool TryGetLegacyCrowdFxMode(JObject settings, out LegacyCrowdFxMode mode)
        {
            mode = default;

            if (!settings.TryGetValue(LEGACY_CROWD_FX_SETTING, out var token))
            {
                return false;
            }

            if (token.Type != JTokenType.Integer && token.Type != JTokenType.String)
            {
                return false;
            }

            return Enum.TryParse(token.ToString(), true, out mode) &&
                mode is LegacyCrowdFxMode.Disabled or LegacyCrowdFxMode.StarpowerClapsOnly or LegacyCrowdFxMode.Enabled;
        }

        private static void MigrateLegacyEffectsMode(JObject settings)
        {
            const string EFFECTS_MODE_SETTING = nameof(SettingsManager.SettingContainer.EffectsMode);
            const string LEGACY_REVERB_SETTING = "ReverbImplementation";

            if (settings.TryGetValue(EFFECTS_MODE_SETTING, out var token))
            {
                if (TryGetInteger(token, out var value) &&
                    value is (int) EffectsMode.Performance or (int) EffectsMode.Quality)
                {
                    return;
                }

                settings[EFFECTS_MODE_SETTING] = (int) EffectsMode.Performance;
                return;
            }

            if (settings.TryGetValue(LEGACY_REVERB_SETTING, out var reverbToken) &&
                TryGetInteger(reverbToken, out var reverbVal) &&
                reverbVal == (int) ReverbMode.Quality)
            {
                settings[EFFECTS_MODE_SETTING] = (int) EffectsMode.Quality;
                return;
            }

            settings[EFFECTS_MODE_SETTING] = (int) EffectsMode.Performance;
        }

        private static bool TryGetInteger(JToken token, out long value)
        {
            value = 0;

            if (token.Type != JTokenType.Integer)
            {
                return false;
            }

            value = (long) token;
            return true;
        }
    }
}
