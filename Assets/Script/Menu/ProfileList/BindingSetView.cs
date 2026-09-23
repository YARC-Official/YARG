using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Navigation;
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
            _profileListMenu.RefreshBindingSetList();
            _profileListMenu.SetSelectedBindingSet(copy);
        }

        public void DeleteBindingSet()
        {
            BindingsContainer.DeleteBindingSet(BindingSet);
            _profileListMenu.RefreshBindingSetList();
            _centerPane.HideContents();
        }
    }
}
