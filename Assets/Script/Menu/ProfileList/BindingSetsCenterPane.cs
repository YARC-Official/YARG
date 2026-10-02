using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;
using YARG.Scores;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsCenterPane : MonoBehaviour
    {
        [SerializeField]
        private ProfilesMenu _profilesMenu;
        [SerializeField]
        private DummyControllerQuickBindDialogMenu _quickBindDialog;
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

        public ReusableBindingSet BindingSet { get; private set; }

        [Space]
        [SerializeField]
        private GameObject _nameContainer;
        [SerializeField]
        private GameObject _editNameContainer;
        [SerializeField]
        private TMP_InputField _nameInput;
        [SerializeField]
        private Button _nameEditButton;

        [Space]
        [SerializeField]
        private BindingsCenterPaneSettingsPanel _settingsPanel;
        private InputDevice _dummyController;

        public InputDevice DummyController {
            get => _dummyController;
            private set {
                if (value != _dummyController)
                {
                    _dummyController = value;
                    DummyControllerChanged?.Invoke();
                }
            }
        }

        private List<InputDevice> _availableDummyControllers = new();

        private bool _showLeftyNames;
        public bool ShowLeftyNames
        {
            get => _showLeftyNames;
            private set
            {
                _showLeftyNames = value;
                HandednessChanged?.Invoke(value);
            }
        }

        public event Action DummyControllerChanged;
        public event Action<bool> HandednessChanged;

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
            HideContents();
        }

        private void OnControllerAdded(InputDevice controller)
        {
            RefreshDummyControllers();
        }

        private void OnControllerRemoved(InputDevice controller)
        {
            if (controller == DummyController)
            {
                DummyController = null;
            }
            RefreshDummyControllers();
        }

        public void ClearBindingSet()
        {
            SelectBindingSet(null);
        }

        public void SelectBindingSet(ReusableBindingSet? bindingSet)
        {
            BindingSet = bindingSet;

            if (BindingSet is null)
            {
                HideContents();
                return;
            }

            ShowContents();

            RefreshFromBindingSet(BindingSet);
        }

        public void SetDummyController()
        {
            var idx = _dummyControllerDropdown.value;

            if (idx < 0 || idx >= _availableDummyControllers.Count)
            {
                DummyController = null;
                return;
            }

            DummyController = _availableDummyControllers[idx];
        }

        public void JumpTo(ReusableBindingSet bindingSet, InputDevice dummyController, bool lefty)
        {
            var controllerIdx = _availableDummyControllers.IndexOf(dummyController);
            if (controllerIdx > 0)
            {
                _dummyControllerDropdown.value = controllerIdx + 1;
                DummyController = dummyController;
            }

            SelectBindingSet(bindingSet);
            _showLeftyNames = lefty;
        }

        private void DestroyBindsList()
        {
            foreach (Transform t in _bindsList)
            {
                if (t != _settingsPanel.transform)
                {
                    Destroy(t.gameObject);
                }
            }
        }

        private void RefreshFromBindingSet(ReusableBindingSet bindingSet)
        {
            _nameEditButton.interactable = !bindingSet.IsHardcoded;

            DestroyBindsList();

            var template = ReusableBindingSetTemplates.GetTemplate(bindingSet.Mode);
            var controls = LayoutHelper.GetAllControlsForControllerFamily(_profilesMenu.CurrentBindingSetFilter);

            foreach (var (action, info) in template)
            {
                switch (info.Type)
                {
                    case BindingType.Button or BindingType.IndividualButton or BindingType.DrumButton:
                        var buttonGroup = Instantiate(_buttonGroupPrefab, _bindsList);
                        buttonGroup.Init(
                            _profilesMenu,
                            this,
                            _quickBindDialog,
                            bindingSet,
                            bindingSet.Bindings[action] as ReusableButtonBinding,
                            controls
                        );
                        break;
                    case BindingType.Axis:
                        var axisGroup = Instantiate(_axisGroupPrefab, _bindsList);
                        axisGroup.Init(
                            _profilesMenu,
                            this,
                            _quickBindDialog,
                            bindingSet,
                            bindingSet.Bindings[action] as ReusableAxisBinding,
                            controls
                        );
                        break;
                    // TODO-FRICK: Integer
                }
            }

            _name.text = bindingSet.Name;
            _settingsPanel.Refresh();
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

            var currentIdx = _availableDummyControllers.IndexOf(DummyController);

            if (currentIdx is -1)
            {
                DummyController = null;
                currentIdx = 0;
            }


            _dummyControllerDropdown.SetValueWithoutNotify(currentIdx);
            _dummyControllerDropdown.RefreshShownValue();
        }

        public void SetHandedness(bool lefty)
        {
            ShowLeftyNames = lefty;
        }

        public void SetNameEditMode(bool editing)
        {
            _nameContainer.SetActive(!editing);
            _editNameContainer.SetActive(editing);

            if (editing)
            {
                _nameInput.text = BindingSet.Name;
                _nameInput.Select();
            }
            else
            {
                // Set the name. Make sure to record the name change in the scores.
                BindingSet.Name = _nameInput.text;

                // Update the UI
                _name.text = BindingSet.Name;
                _profilesMenu.GetSelectedBindingSetView().UpdateDisplay(BindingSet);
            }
        }
    }
}
