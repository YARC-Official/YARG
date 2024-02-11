using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings.AllSettings
{
    public class SettingSearchResult : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _settingText;

        public void Initialize(string localizedName)
        {
            _settingText.text = localizedName;
        }
    }
}