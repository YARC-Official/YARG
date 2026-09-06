using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.ProfileList
{
    public class BindingSetView : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _bindingSetName;

        public ReusableBindingSet BindingSet { get; private set; }
        private ProfilesMenu _profileListMenu;
        private ProfileCenterPane _profileCenterPane;

        public void Init(ProfilesMenu menu, ReusableBindingSet bindingSet, ProfileCenterPane centerPane)
        {
            _profileListMenu = menu;
            _profileCenterPane = centerPane;
            UpdateDisplay(bindingSet);
        }

        public void UpdateDisplay(ReusableBindingSet bindingSet)
        {
            BindingSet = bindingSet;
            _bindingSetName.text = bindingSet.Name;
        }
    }
}
