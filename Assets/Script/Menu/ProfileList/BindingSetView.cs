using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Input.Bindings;
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

        private ReusableBindingSet _bindingSet;
        private ProfilesMenu _profileListMenu;

        public void Init(ProfilesMenu menu, ReusableBindingSet bindingSet, BindingSetsCenterPane centerPane)
        {
            _profileListMenu = menu;
            _centerPane = centerPane;
            _bindingSet = bindingSet;
            UpdateDisplay(bindingSet);
        }

        public void UpdateDisplay(ReusableBindingSet bindingSet)
        {
            _bindingSetName.text = bindingSet.Name;
        }

        protected override void OnSelectionChanged(bool selected)
        {
            base.OnSelectionChanged(selected);

            if (selected)
            {
                _centerPane.SelectBindingSet(_bindingSet, this);
            }
        }
    }
}
