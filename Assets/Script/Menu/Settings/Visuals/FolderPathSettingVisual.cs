using TMPro;
using UnityEngine;
using YARG.Gameplay;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class FolderPathSettingVisual : BaseSettingVisual<FolderPathSetting>
    {
        [SerializeField]
        private TextMeshProUGUI _pathText;

        public override NavigationScheme GetNavigationScheme() => NavigationScheme.Empty;

        public override void RefreshVisual()
        {
            if (string.IsNullOrEmpty(Setting.Value))
            {
                // Empty means "auto", so show what auto-detection actually found rather than
                // just saying "auto" -- otherwise there's no way to tell from the settings menu
                // whether video backgrounds are even going to work.
                var autoDetected = LibVlcNativePath.GetAutoDetectedPath();
                _pathText.text = autoDetected ?? Localize.Key("Menu.Settings.NotFound");
            }
            else
            {
                _pathText.text = Setting.Value;
            }
        }

        public void Browse()
        {
            var startingDir = Setting.Value;
            FileExplorerHelper.OpenChooseFolder(startingDir, folder =>
            {
                Setting.Value = folder;
                RefreshVisual();
            });
        }

        public void Reset()
        {
            Setting.Value = string.Empty;
            RefreshVisual();
        }
    }
}
