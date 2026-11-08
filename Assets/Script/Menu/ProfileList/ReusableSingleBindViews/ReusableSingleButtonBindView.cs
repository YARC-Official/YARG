using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using YARG.Assets.Script.Menu.ProfileList;
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleButtonBindView : ReusableSingleBindView<ReusableButtonBinding, bool, ReusableSingleButtonBinding, float>
    {
        [SerializeField]
        private AxisDisplay _valueDisplay;
        [SerializeField]
        private ButtonDisplay _pressedIndicator;

        [Space]
        [SerializeField]
        private GameObject _invertGroup;
        [SerializeField]
        private Toggle _invertToggle;
        [SerializeField]
        private TextMeshProUGUI _pressPointText;
        [SerializeField]
        private ValueSlider _pressPointSlider;
        [SerializeField]
        private TMP_Dropdown _debounceModeDropdown;
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(
            ReusableBindGroup bindGroup,
            ReusableButtonBinding binding,
            ReusableSingleButtonBinding singleBinding,
            List<ControlItemInfo> controls,
            ProfilesMenu profilesMenu,
            BindingSetsCenterPane centerPane,
            DummyControllerRecordDialogMenu quickBindDialog,
            bool interactable
        )
        {
            base.Init(bindGroup, binding, singleBinding, controls, profilesMenu, centerPane, quickBindDialog, interactable);

            // Set with notify for propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _pressPointSlider.SetValueWithoutNotify(GetDisplayedPressPoint());
            _debounceModeDropdown.value = (int) singleBinding.DebounceMode;
            _debounceSlider.Value = singleBinding.DebounceThreshold;

            _invertToggle.interactable = interactable;
            _pressPointSlider.SetInteractable(interactable);
            _debounceSlider.SetInteractable(interactable);
            _debounceModeDropdown.interactable = interactable;

        }

        public void OnInvertChanged()
        {
            SingleBinding.Inverted = _invertToggle.isOn;
        }

        public void OnPressPointChanged()
        {
            var value = _pressPointSlider.Value;
            SingleBinding.PressPoint = value;
            _valueDisplay.PressPoint = value;
        }

        public void OnDebounceModeChanged()
        {
            SingleBinding.DebounceMode = (DebounceMode) _debounceModeDropdown.value;
        }

        public void OnDebounceValueChanged()
        {
            SingleBinding.DebounceThreshold = (long) _debounceSlider.Value;
        }

        protected override void PopulateControlDropdown()
        {
            base.PopulateControlDropdown();

            foreach (var control in _allControls)
            {
                if (BindingSetHelper.IsControlValidForButton(control))
                {
                    _dropdownControls.Add(new(control));
                    _controlDropdown.options.Add(new(DisambiguateDisplayName(control)));
                }
            }
        }

        public override void OnControlDropdownChange()
        {
            base.OnControlDropdownChange();
            UpdateAxisDisplayThreshold();

            if (
                (_current is null && _profilesMenu.CurrentBindingSetFilter is ControllerFamily.MidiDevice) ||
                (_current is not null && _current.Value.Layout is LayoutStrings.MIDI_NOTE)
            )
            {
                SingleBinding.Inverted = false;
                _invertToggle.isOn = false;
                _invertGroup.SetActive(false);
                _pressPointText.text = Localize.Key("Menu.ProfileList.VelocityThreshold");
            }
            else
            {
                _invertGroup.SetActive(true);
                _pressPointText.text = Localize.Key("Menu.ProfileList.PressPoint");
            }
        }

        // Unsure if this is too hacky/hardcoded
        // Also should really be localized
        protected override string DisambiguateDisplayName(ControlItemInfo item)
        {
            switch (item.ParentLayout)
            {
                case LayoutStrings.DPAD:
                    return $"D-Pad {item.DisplayName}";
                case LayoutStrings.STICK:
                    return $"Joystick {item.DisplayName}";
            }

            if (item.ControlPath == "joystickClick")
            {
                return "Joystick Click";
            }

            return item.DisplayName;
        }

        protected override bool UpdateDummyInputVisuals(InputControl<float> dummyControl)
        {
            var calibrated = dummyControl.value * (SingleBinding.Inverted ? -1 : 1);
            _valueDisplay.Value = calibrated;
            var pressPoint = GetEffectivePressPoint(dummyControl);
            _valueDisplay.PressPoint = pressPoint;
            var pressed = calibrated >= pressPoint;
            _pressedIndicator.IsPressed = pressed;
            return pressed;
        }

        private void UpdateAxisDisplayThreshold()
        {
            var pressPoint = GetEffectivePressPoint(_dummyInputControl as InputControl<float>);
            _valueDisplay.PressPoint = pressPoint;
        }

        private float GetEffectivePressPoint(InputControl<float> control)
        {
            if (SingleBinding.HasPressPointOverride)
            {
                return SingleBinding.PressPoint;
            }

            return control is ButtonControl button
                ? button.pressPointOrDefault
                : GetDisplayedPressPoint();
        }

        private float GetDisplayedPressPoint()
        {
            if (SingleBinding.HasPressPointOverride)
            {
                return SingleBinding.PressPoint;
            }

            return _current?.DefaultPressPoint ?? ReusableSingleButtonBinding.PRESS_POINT_DEFAULT;
        }
    }
}
