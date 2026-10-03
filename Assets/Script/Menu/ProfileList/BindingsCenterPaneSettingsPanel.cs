using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
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

        private List<YargProfile> _allUsers = new();
        public IReadOnlyList<YargProfile> AllUsers => _allUsers;

        private List<YargProfile> _activeUsers = new();
        public IReadOnlyList<YargProfile> ActiveUsers => _activeUsers;

        public bool Locked { get; private set; }

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

                GetUserCounts(bindingSet);

                Locked = _allUsers.Count > 1;

                const string localizationKeyPrefix = "Menu.ProfileList.UserCount.";

                _userCountText.text = _allUsers.Count switch
                {
                    0 => Localize.Key($"{localizationKeyPrefix}Zero"),
                    1 => Localize.KeyFormat($"{localizationKeyPrefix}One", _allUsers[0].Name),
                    _ => Localize.KeyFormat($"{localizationKeyPrefix}Multiple", _allUsers.Count)
                };
            }
        }

        private (List<YargProfile> allUsers, List<YargProfile> activeUsers) GetUserCounts(ReusableBindingSet bindingSet)
        {
            _allUsers.Clear();
            _activeUsers.Clear();

            foreach (var deviceInfo in BindingsContainer.AllPlayerDeviceInfo)
            {
                // Check preferred binding sets (one per (ControllerFamily,GameMode) tuple)
                var profileBindings = deviceInfo.AllPreferredBindingSets;
                if (profileBindings.Contains(bindingSet))
                {
                    _allUsers.Add(deviceInfo.Profile);

                    if (PlayerContainer.IsProfileTaken(deviceInfo.Profile)) {
                        _activeUsers.Add(deviceInfo.Profile);
                    }

                    continue;
                }

                // If a connected player has multiple controllers of the same family at the same
                // time, then some of them might be using other binding sets besides that player's
                // general (ControllerFamily,GameMode)-wide preference
                if (deviceInfo.BindingSetsInUse.Contains(bindingSet))
                {
                    _allUsers.Add(deviceInfo.Profile);

                    // Disconnected profiles will always have an empty BindingSetsInUse, so no need
                    // to check IsProfileTaken
                    _activeUsers.Add(deviceInfo.Profile); ;
                }
            }

            return (_allUsers, _activeUsers);
        }
    }
}
