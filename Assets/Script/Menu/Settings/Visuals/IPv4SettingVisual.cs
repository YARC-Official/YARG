using System.Net;
using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class IPv4SettingVisual : BaseSettingVisual<IPv4Setting>
    {
        [SerializeField]
        private TMP_InputField _inputField;

        protected override void RefreshVisual()
        {
            _inputField.text = Setting.Value;
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish
            }, true);
        }

        public void OnTextFieldChange()
        {
            try
            {
                if (IPAddress.TryParse(_inputField.text, out var ipAddress))
                {
                    if (IPv4Setting.IsValidIPv4(ipAddress))
                    {
                        Setting.Value = ipAddress.ToString();
                    }
                }
            }
            catch
            {
                // Ignore error
            }

            RefreshVisual();
        }
    }
}

/*
"If you're a cowboy and you're dragging a man behind your horse, I bet it would really make you mad if you looked back and the guy was reading a magazine."

- Jack Handey.
*/
