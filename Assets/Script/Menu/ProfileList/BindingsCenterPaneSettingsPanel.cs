using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class BindingsCenterPaneSettingsPanel : MonoBehaviour
    {
        [SerializeField]
        private Button _quickBindButton;
        [SerializeField]
        private GameObject _leftyNamesGroup;
        [SerializeField]
        private Toggle _leftyToggle;
        [SerializeField]
        private GameObject _userCountGroup;
        [SerializeField]
        private Image _userCountBackground;
        [SerializeField]
        private TextMeshProUGUI _userCountText;
        [SerializeField]
        private GameObject _unlockButton;
        [SerializeField]
        private BindingSetsCenterPane _centerPane;

        private List<YargProfile> _allUsers = new();
        public IReadOnlyList<YargProfile> AllUsers => _allUsers;

        private List<YargProfile> _activeUsers = new();
        public IReadOnlyList<YargProfile> ActiveUsers => _activeUsers;

        public bool Locked { get; private set; }
        private bool _explicitlyUnlocked = false;

        public void OnEnable()
        {
            Refresh();
            _centerPane.HandednessChanged += (_) => Refresh();
        }

        public event Action Unlocked;

        public void Unlock()
        {
            Locked = false;
            _explicitlyUnlocked = true;
            Refresh();
            Unlocked?.Invoke();
        }
        public void Refresh()
        {
            _quickBindButton.interactable = _centerPane.BindingSet is not null &&
                !_centerPane.BindingSet.IsHardcoded &&
                _centerPane.DummyController is not null;

            _leftyToggle.SetIsOnWithoutNotify(_centerPane.ShowLeftyNames);

            var bindingSet = _centerPane.BindingSet;

            if (bindingSet is null)
            {
                _leftyNamesGroup.SetActive(false);
                _userCountGroup.SetActive(false);
                return;
            }

            _leftyNamesGroup.SetActive(bindingSet.Mode.HasLeftyNames());

            if (bindingSet.IsHardcoded)
            {
                _userCountGroup.SetActive(false);
            }
            else
            {
                _userCountGroup.SetActive(true);

                (_allUsers, _activeUsers) = BindingSetHelper.GetUsersOfBindingSet(bindingSet);

                Locked = !_explicitlyUnlocked && _allUsers.Count > 1;

                const string localizationKeyPrefix = "Menu.ProfileList.UserCount.";

                _userCountText.text = _allUsers.Count switch
                {
                    0 => Localize.Key($"{localizationKeyPrefix}Zero"),
                    1 => Localize.KeyFormat($"{localizationKeyPrefix}One{(PlayerContainer.IsProfileTaken(_allUsers[0]) ? "Active" : "Inactive")}", _allUsers[0].Name),
                    _ => Localize.KeyFormat($"{localizationKeyPrefix}Multiple{(Locked ? "Locked" : "Unlocked")}", _allUsers.Count)
                };

                _unlockButton.SetActive(Locked);
                _userCountBackground.color = _userCountBackground.color.WithAlpha(Locked ? 1 : 0);
            }
        }
    }
}
