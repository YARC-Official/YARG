using System.Globalization;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class IPv4SettingVisual : BaseSettingVisual<IPv4Setting>
    {
        [SerializeField]
        private TMP_InputField[] _inputField;

        protected override void RefreshVisual()
        {
            for (int i = 0; i < Setting.Value.Length; i++)
            {
                _inputField[i].text = Setting.Value[i].ToString(CultureInfo.InvariantCulture);
            }
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Increase", () =>
                {
                    // need to change this to the correct index
                    Setting.Value[0]++;
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Decrease", () =>
                {
                    // need to change this to the correct index
                    Setting.Value[0]--;
                    RefreshVisual();
                })
            }, true);
        }

        public void OnTextFieldChange(int index)
        {
            try
            {
                byte value = byte.Parse(_inputField[index].text, CultureInfo.InvariantCulture);
                value = (byte) Mathf.Clamp(value, Setting.Min, Setting.Max);
                Setting.Value[index] = value;
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
