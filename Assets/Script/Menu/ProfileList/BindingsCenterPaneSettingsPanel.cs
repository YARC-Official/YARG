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

        public void OnEnable()
        {
            _centerPane.HandednessChanged += (_) => Refresh();
        }

        public void Refresh()
        {
            _leftyToggle.SetIsOnWithoutNotify(_centerPane.ShowLeftyNames);

            var bindingSet = _centerPane.BindingSet;

            _leftyNamesGroup.SetActive(bindingSet.Mode.HasLeftyNames());

            if (bindingSet.IsHardcoded)
            {
                _userCountGroup.SetActive(false);
            }
            else
            {
                _userCountGroup.SetActive(true);

                var userCount = GetUserCount(bindingSet);

                var userCountNumText = $"<color=#{(userCount > 1 ? "FF0000" : "FFFFFF")}>{userCount}</color>";

                _userCountText.text = Localize.KeyFormat("Menu.ProfileList.UserCount", userCountNumText);
            }
        }

        private int GetUserCount(ReusableBindingSet bindingSet)
        {
            var userCount = 0;

            foreach (var deviceInfo in BindingsContainer.AllPlayerDeviceInfo)
            {
                // Check preferred binding sets (one per (ControllerFamily,GameMode) tuple)
                var profileBindings = deviceInfo.AllPreferredBindingSets;
                if (profileBindings.Contains(bindingSet))
                {
                    userCount++;
                    continue;
                }

                // If a connected player has multiple controllers of the same family at the same
                // time, then some of them might be using other binding sets besides that player's
                // general (ControllerFamily,GameMode)-wide preference
                if (deviceInfo.BindingSetsInUse.Contains(bindingSet))
                {
                    userCount++;
                }
            }

            return userCount;
        }
    }
}
