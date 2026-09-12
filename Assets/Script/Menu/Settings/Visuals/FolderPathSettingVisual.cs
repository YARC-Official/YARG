using TMPro;
using UnityEngine;
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
            _pathText.text = string.IsNullOrEmpty(Setting.Value)
                ? Localize.Key("Menu.Settings.UsingDefaultVlcPath")
                : Setting.Value;
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
