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

        private ReusableBindingSet _bindingSet;
        private ProfilesMenu _profileListMenu;

        public void Init(ProfilesMenu menu, ReusableBindingSet bindingSet, BindingSetsCenterPane centerPane)
        {
            _profileListMenu = menu;
            _centerPane = centerPane;
            _bindingSet = bindingSet;
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
                _centerPane.SelectBindingSet(_bindingSet);
            }
        }

        public void CopyBindingSet()
        {
            var copy = new ReusableBindingSet(_bindingSet);

            BindingsContainer.AddBindingSet(copy);
            _profileListMenu.RefreshBindingSetList();
        }

        public void DeleteBindingSet()
        {
            BindingsContainer.DeleteBindingSet(_bindingSet);
            _profileListMenu.RefreshBindingSetList();
        }
    }
}
