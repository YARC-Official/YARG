using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsCenterPane : MonoBehaviour
    {
        [SerializeField]
        private ProfilesMenu _profilesMenu;
        [SerializeField]
        private GameObject _contents;
        [SerializeField]
        private Transform _bindsList;
        [SerializeField]
        private TextMeshProUGUI _name;

        [Space]
        [SerializeField]
        private ReusableButtonBindGroup _buttonGroupPrefab;
        [SerializeField]
        private ReusableAxisBindGroup _axisGroupPrefab;
        [SerializeField]
        private IntegerBindGroup _integerGroupPrefab;

        private ReusableBindingSet _bindingSet;
        private BindingSetView _bindingSetView;

        public void HideContents()
        {
            _contents.SetActive(false);
        }

        public void ShowContents()
        {
            _contents.SetActive(true);
        }

        public void OnEnable()
        {
            ClearBindingSet();
        }

        public void ClearBindingSet()
        {
            SelectBindingSet(null, null);
        }

        public void SelectBindingSet(ReusableBindingSet? bindingSet, BindingSetView bindingSetView)
        {
            _bindingSet = bindingSet;
            _bindingSetView = bindingSetView;

            if (_bindingSet is null)
            {
                HideContents();
                return;
            }

            ShowContents();

            RefreshFromBindingSet(_bindingSet);
        }

        private void RefreshFromBindingSet(ReusableBindingSet bindingSet)
        {
            _bindsList.DestroyChildren();

            var template = ReusableBindingSetTemplates.GetTemplate(bindingSet.Mode);
            var controls = LayoutHelper.GetAllControlsForControllerFamily(_profilesMenu.CurrentBindingSetFilter);

            foreach (var (action, info) in template)
            {
                switch (info.Type)
                {
                    case BindingType.Button or BindingType.IndividualButton or BindingType.DrumButton:
                        var buttonGroup = Instantiate(_buttonGroupPrefab, _bindsList);
                        buttonGroup.Init(this, bindingSet, bindingSet.Bindings[action] as ReusableButtonBinding, controls);
                        break;
                    case BindingType.Axis:
                        var axisGroup = Instantiate(_axisGroupPrefab, _bindsList);
                        axisGroup.Init(this, bindingSet, bindingSet.Bindings[action] as ReusableAxisBinding, controls);
                        break;
                }
            }

            _name.text = bindingSet.Name;
        }
    }
}
