using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleButtonBindView : ReusableSingleBindView<ReusableButtonBinding, ReusableSingleButtonBinding, float>
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

        public override void Init(ReusableButtonBinding binding, ReusableSingleButtonBinding singleBinding, List<ControlItemInfo> controls, ProfilesMenu profilesMenu)
        {
            base.Init(binding, singleBinding, controls, profilesMenu);

            // Set with notify for propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _pressPointSlider.Value = singleBinding.PressPoint;
            _debounceModeDropdown.value = (int) singleBinding.DebounceMode;
            _debounceSlider.Value = singleBinding.DebounceThreshold;
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
                if (
                    (control.Layout is LayoutStrings.BUTTON or LayoutStrings.MIDI_NOTE or LayoutStrings.KEY) ||
                    (control.Layout is LayoutStrings.AXIS && control.ParentPath is null) // *
                    // * e.g., D-pad X- and Y-axes are not okay, because they're just aggregates of buttons
                    //   but Tilt is okay, because it's an axis in its own right
                )
                {
                    _dropdownControls.Add(control);
                    _controlDropdown.options.Add(new(DisambiguateDisplayName(control)));
                }
            }
        }

        public override void OnControlDropdownChange()
        {
            base.OnControlDropdownChange();

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
    }
}
