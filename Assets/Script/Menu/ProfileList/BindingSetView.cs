using TMPro;
using UnityEngine;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Navigation;

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
        }
    }
}
