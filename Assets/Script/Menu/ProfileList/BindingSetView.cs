using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings.Metadata;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;

namespace YARG.Menu.ProfileList
{
    public class BindingSetView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindingSetName;
        [SerializeField]
        private BindingSetsCenterPane _centerPane;
        [SerializeField]
        private GameObject _deleteButton;

        public ReusableBindingSet BindingSet { get; private set; }
        private ProfilesMenu _profileListMenu;

        public void Init(ProfilesMenu menu, ReusableBindingSet bindingSet, BindingSetsCenterPane centerPane)
        {
            _profileListMenu = menu;
            _centerPane = centerPane;
            BindingSet = bindingSet;
            UpdateDisplay(bindingSet);

            _deleteButton.SetActive(!bindingSet.IsHardcoded);
        }

        public void UpdateDisplay(ReusableBindingSet bindingSet)
        {
            _bindingSetName.text = bindingSet.Name +
                (bindingSet.IsHardcoded ?
                    $" <i><sup>({Localize.Key("Bindings.Hardcoded")})</sup></i>" :
                    string.Empty
                );
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                _centerPane.SelectBindingSet(BindingSet);
            }
        }

        public void CopyBindingSet()
        {
            var copy = new ReusableBindingSet(BindingSet);

            BindingsContainer.AddBindingSet(copy);
            _profileListMenu.RefreshBindingSetList(copy);
        }

        public void DeleteBindingSet()
        {
            var (allUsers, activeUsers) = BindingSetHelper.GetUsersOfBindingSet(BindingSet);

            float armDelaySeconds = allUsers.Count is 0 ? 0f : 2f;

            const string messageKeyPrefix = "Menu.ProfileList.DeleteBindingSet.";
            string message = allUsers.Count switch {
                0 => Localize.Key($"{messageKeyPrefix}Zero"),
                1 => Localize.KeyFormat($"{messageKeyPrefix}One{(activeUsers.Count > 0 ? "Active" : "Inactive")}", allUsers[0].Name),
                _ => Localize.KeyFormat($"{messageKeyPrefix}Multiple", allUsers.Count.ToString())
            };

            PresetSubTab.ShowCompactConfirmation(
                Localize.KeyFormat("Menu.Dialog.ConfirmDelete.Title", BindingSet.Name),
                message,
                "Menu.Common.Delete",
                MenuData.Colors.CancelButton,
                () =>
                {
                    DialogManager.Instance.ClearDialog();
                    var selectedBindingSet = _profileListMenu.GetSelectedBindingSet();
                    var wasSelected = Selected;

                    BindingsContainer.DeleteBindingSet(BindingSet);

                    if (wasSelected)
                    {
                        _centerPane.ClearBindingSet();
                        _profileListMenu.RefreshBindingSetList();
                    }
                    else
                    {
                        _profileListMenu.RefreshBindingSetList(selectedBindingSet);
                    }
                },
                cancelColor: MenuData.Colors.BrightButton,
                armDelaySeconds: armDelaySeconds
            );
        }
    }
}
