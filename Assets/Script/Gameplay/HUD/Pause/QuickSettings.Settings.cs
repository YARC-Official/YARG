using System.Collections.Generic;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public partial class QuickSettings
    {
        private static readonly List<string> _soundSettings = new()
        {
            nameof(SettingsManager.Settings.MasterMusicVolume),
            nameof(SettingsManager.Settings.GuitarVolume),
            nameof(SettingsManager.Settings.RhythmVolume),
            nameof(SettingsManager.Settings.BassVolume),
            nameof(SettingsManager.Settings.KeysVolume),
            nameof(SettingsManager.Settings.DrumsVolume),
            nameof(SettingsManager.Settings.VocalsVolume),
            nameof(SettingsManager.Settings.SongVolume),
            nameof(SettingsManager.Settings.CrowdVolume),
            nameof(SettingsManager.Settings.SfxVolume),
            nameof(SettingsManager.Settings.VocalMonitoring),
        };
    }
}