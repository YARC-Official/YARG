using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input;
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

        [Space]
        [SerializeField]
        private TMP_Dropdown _dummyControllerDropdown;

        private ReusableBindingSet _bindingSet;

        [Space]
        [SerializeField]
        private Transform _settingsPanel;
        private InputDevice _dummyController = null;
        private List<InputDevice> _availableDummyControllers = new();

        public void HideContents()
        {
            _contents.SetActive(false);
        }

        public void ShowContents()
        {
            _contents.SetActive(true);
            RefreshDummyControllers();
        }

        public void OnEnable()
        {
            InputManager.DeviceAdded += OnControllerAdded;
            InputManager.DeviceRemoved += OnControllerRemoved;
        }

        public void OnDisable()
        {
            InputManager.DeviceAdded -= OnControllerAdded;
            InputManager.DeviceRemoved -= OnControllerRemoved;
        }

        private void OnControllerAdded(InputDevice controller)
        {
            RefreshDummyControllers();
        }

        private void OnControllerRemoved(InputDevice controller)
        {
            if (controller == _dummyController)
            {
                _dummyController = null;
            }
            RefreshDummyControllers();
        }

        public void ClearBindingSet()
        {
            SelectBindingSet(null);
        }

        public void SelectBindingSet(ReusableBindingSet? bindingSet)
        {
            _bindingSet = bindingSet;

            if (_bindingSet is null)
            {
                HideContents();
                return;
            }

            ShowContents();

            RefreshFromBindingSet(_bindingSet);
        }

        public void SetDummyController()
        {
            var idx = _dummyControllerDropdown.value;

            if (idx < 0 || idx >= _availableDummyControllers.Count)
            {
                _dummyController = null;
                return;
            }

            _dummyController = _availableDummyControllers[idx];
        }

        private void DestroyBindsList()
        {
            foreach (Transform t in _bindsList)
            {
                if (t != _settingsPanel)
                {
                    Destroy(t.gameObject);
                }
            }
        }

        private void RefreshFromBindingSet(ReusableBindingSet bindingSet)
        {
            DestroyBindsList();

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

        public void RefreshDummyControllers()
        {
            _dummyControllerDropdown.options.Clear();
            _dummyControllerDropdown.options.Add(new("<i>None</i>"));

            _availableDummyControllers.Clear();
            _availableDummyControllers.Add(null);

            var family = _profilesMenu.CurrentBindingSetFilter;

            foreach (var controller in InputSystem.devices)
            {
                if (family == LayoutHelper.LayoutStringToControllerFamily(controller.layout))
                {
                    _availableDummyControllers.Add(controller);
                    _dummyControllerDropdown.options.Add(new(controller.displayName));
                }
            }

            var currentIdx = _availableDummyControllers.IndexOf(_dummyController);

            if (currentIdx is -1)
            {
                _dummyController = null;
                currentIdx = 0;
            }


            _dummyControllerDropdown.SetValueWithoutNotify(currentIdx);
            _dummyControllerDropdown.RefreshShownValue();
        }
    }
}
