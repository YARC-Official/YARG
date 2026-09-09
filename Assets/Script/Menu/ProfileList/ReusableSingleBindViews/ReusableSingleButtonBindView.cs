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
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleButtonBindView : ReusableSingleBindView<ReusableButtonBinding, ReusableSingleButtonBinding>
    {
        [SerializeField]
        private AxisDisplay _valueDisplay;
        [SerializeField]
        private ButtonDisplay _pressedIndicator;

        [Space]
        [SerializeField]
        private Toggle _invertToggle;
        [SerializeField]
        private ValueSlider _pressPointSlider;
        [SerializeField]
        private TMP_Dropdown _debounceModeDropdown;
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(ReusableButtonBinding binding, ReusableSingleButtonBinding singleBinding, List<ControlItemInfo> controls)
        {
            base.Init(binding, singleBinding, controls);

            // Set with notify for propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _pressPointSlider.Value = singleBinding.PressPoint;
            _debounceModeDropdown.value = (int) singleBinding.DebounceMode;
            _debounceSlider.Value = singleBinding.DebounceThreshold;
        }

        public void OnInvertChanged(bool value)
        {
            SingleBinding.Inverted = value;
        }

        public void OnPressPointChanged(float value)
        {
            SingleBinding.PressPoint = value;
            _valueDisplay.PressPoint = value;
        }

        public void OnDebounceModeChanged(int value)
        {
            SingleBinding.DebounceMode = (DebounceMode) value;
        }

        public void OnDebounceValueChanged(float value)
        {
            SingleBinding.DebounceThreshold = (long) value;
        }

        protected override void PopulateControlDropdown()
        {
            base.PopulateControlDropdown();

            foreach (var control in _controls)
            {
                if (
                    control.Layout == LayoutStrings.BUTTON ||
                    (control.Layout == LayoutStrings.AXIS && control.ParentPath is null)
                )
                {
                    _controlDropdown.options.Add(new(control.DisplayName));
                }
            }
        }
    }
}
