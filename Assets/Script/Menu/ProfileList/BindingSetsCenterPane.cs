using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UniGLTF.Extensions.VRMC_vrm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Menu.Persistent;
using YARG.Menu.ProfileInfo;
using YARG.Player;
using YARG.Scores;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsCenterPane : MonoBehaviour
    {
        [SerializeField]
        private ProfilesMenu _profilesMenu;
        [SerializeField]
        private DummyControllerRecordDialogMenu _quickBindDialog;
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
        private ReusableIntegerBindGroup _integerGroupPrefab;

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
        private Button _exportButton;

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
                    _settingsPanel.Refresh();
                    UpdateDummyControllerMenuSuppression();
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

        public async void ShowQuickBind()
        {
            if (BindingSet.Mode is GameMode.FourLaneDrums or GameMode.ProKeys or
                    GameMode.FiveLaneDrums or GameMode.EliteDrums)
            {
                var dialog = DialogManager.Instance.ShowFriendlyBindingDialog(BindingSet, DummyController, _showLeftyNames);

                if (dialog is not null)
                {
                    await dialog.WaitUntilClosed();
                    RefreshFromBindingSet(BindingSet);
                }
            }
            else
            {
                var dialog = DialogManager.Instance.ShowMessage("Unsupported Instrument Type",
                    "Quick binding is currently only supported for Drums and Keys.");

                if (dialog is not null)
                {
                    await dialog.WaitUntilClosed();
                }
            }
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
            _exportButton.interactable = !bindingSet.IsHardcoded;

            DestroyBindsList();

            var template = ReusableBindingSetTemplates.GetTemplate(bindingSet.Mode);
            var controls = LayoutHelper.GetAllControlsForControllerFamily(_profilesMenu.CurrentBindingSetFilter);

            foreach (var (action, info) in template)
            {
                switch (info.Type)
                {
                    case BindingType.Button or BindingType.Impulse:
                        var buttonGroup = Instantiate(_buttonGroupPrefab, _bindsList);
                        buttonGroup.Init(
                            _profilesMenu,
                            this,
                            _settingsPanel,
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
                            _settingsPanel,
                            _quickBindDialog,
                            bindingSet,
                            bindingSet.Bindings[action] as ReusableAxisBinding,
                            controls
                        );
                        break;
                    case BindingType.Integer:
                        var integerGroup = Instantiate(_integerGroupPrefab, _bindsList);
                        integerGroup.Init(
                            _profilesMenu,
                            this,
                            _settingsPanel,
                            _quickBindDialog,
                            bindingSet,
                            bindingSet.Bindings[action] as ReusableIntegerBinding,
                            controls
                        );
                        break;
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

        public void Export()
        {
            FileExplorerHelper.OpenSaveFile(null, BindingSet.Name, "binds", path =>
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                try
                {
                    var serializable = BindingSet.Serialize(export: true);
                    var text = JsonConvert.SerializeObject(
                        serializable,
                        new JsonSerializerSettings() {
                            Formatting = Formatting.Indented,
                            NullValueHandling = NullValueHandling.Ignore
                        }
                    );
                    File.WriteAllText(path, text);
                }
                catch (Exception) {
                    YargLogger.LogError("Failed to export binding set.");
                }
            });
        }

        private void UpdateDummyControllerMenuSuppression()
        {
            if (_dummyController is not null)
            {
                foreach (var player in PlayerContainer.Players)
                {
                    if (player.DeviceInfo.ContainsController(_dummyController))
                    {
                        player.DeviceInfo.SuppressDummyControllerMenuInputs(_dummyController);
                        return;
                    }
                }
            }
            else
            {
                foreach (var player in PlayerContainer.Players)
                {
                    player.DeviceInfo.UnsuppressDummyControllerMenuInputs();
                }
            }
        }
    }
}
