using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class BindingsCenterPaneSettingsPanel : MonoBehaviour
    {
        [SerializeField]
        private GameObject _leftyNamesGroup;
        [SerializeField]
        private Toggle _leftyToggle;
        [SerializeField]
        private GameObject _userCountGroup;
        [SerializeField]
        private TextMeshProUGUI _userCountText;
        [SerializeField]
        private BindingSetsCenterPane _centerPane;


        public void Refresh()
        {
            _leftyToggle.SetIsOnWithoutNotify(_centerPane.ShowLeftyNames);

            var bindingSet = _centerPane.BindingSet;

            _leftyNamesGroup.SetActive(bindingSet.Mode.HasLeftyNames() || bindingSet.ControllerFamily.HasLeftyNames());

            if (bindingSet.IsHardcoded)
            {
                _userCountGroup.SetActive(false);
            }
            else
            {
                _userCountGroup.SetActive(true);

                var userCount = 0;

                foreach (var deviceInfo in BindingsContainer.AllPlayerDeviceInfo)
                {
                    var profileBindings = deviceInfo.AllPreferredBindingSets;
                    if (profileBindings.Contains(bindingSet))
                    {
                        userCount++;
                    }
                }

                var userCountNumText = $"<color=#{(userCount > 1 ? "FF0000" : "FFFFFF")}>{userCount}</color>";

                _userCountText.text = Localize.KeyFormat("Menu.ProfileList.UserCount", userCountNumText);
            }
        }

        public void OnChangeHandedness()
        {
            _centerPane.SetHandedness(_leftyToggle.isOn);
        }
    }
}
