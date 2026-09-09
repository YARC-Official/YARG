using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleAxisBindView : ReusableSingleBindView<ReusableAxisBinding, ReusableSingleAxisBinding>
    {
        [SerializeField]
        private AxisDisplay _rawValueDisplay;
        [SerializeField]
        private AxisDisplay _calibratedValueDisplay;

        [Space]
        [SerializeField]
        private Toggle _invertToggle;
        [SerializeField]
        private ValueSlider _maxValueSlider;
        [SerializeField]
        private ValueSlider _minValueSlider;
        [SerializeField]
        private ValueSlider _upperDeadzoneSlider;
        [SerializeField]
        private ValueSlider _lowerDeadzoneSlider;

        public override void Init(ReusableAxisBinding binding, ReusableSingleAxisBinding singleBinding, List<ControlItemInfo> controls)
        {
            base.Init(binding, singleBinding, controls);

            // Set with notify for value corrections and propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _maxValueSlider.Value = singleBinding.Maximum;
            _minValueSlider.Value = singleBinding.Minimum;
            _upperDeadzoneSlider.Value = singleBinding.UpperDeadzone;
            _lowerDeadzoneSlider.Value = singleBinding.LowerDeadzone;
        }

        public void OnInvertChanged(bool value)
        {
            SingleBinding.Inverted = value;
        }

        public void OnMaxValueChanged(float value)
        {
            SingleBinding.Maximum = value;
            _rawValueDisplay.Maximum = value;
            _calibratedValueDisplay.Maximum = value;

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;

            if (value < SingleBinding.UpperDeadzone)
                _upperDeadzoneSlider.Value = value;

            if (value < SingleBinding.LowerDeadzone)
                _lowerDeadzoneSlider.Value = value;
        }

        public void OnMinValueChanged(float value)
        {
            SingleBinding.Minimum = value;
            _rawValueDisplay.Minimum = value;
            _calibratedValueDisplay.Minimum = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value > SingleBinding.LowerDeadzone)
                _lowerDeadzoneSlider.Value = value;

            if (value > SingleBinding.UpperDeadzone)
                _upperDeadzoneSlider.Value = value;
        }

        public void OnUpperDeadzoneChanged(float value)
        {
            SingleBinding.UpperDeadzone = value;
            _rawValueDisplay.UpperDeadzone = value;
            _calibratedValueDisplay.UpperDeadzone = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;

            if (value < SingleBinding.LowerDeadzone)
                _lowerDeadzoneSlider.Value = value;
        }

        public void OnLowerDeadzoneChanged(float value)
        {
            SingleBinding.LowerDeadzone = value;
            _rawValueDisplay.LowerDeadzone = value;
            _calibratedValueDisplay.LowerDeadzone = value;

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value > SingleBinding.UpperDeadzone)
                _upperDeadzoneSlider.Value = value;
        }

        protected override void PopulateControlDropdown()
        {
            base.PopulateControlDropdown();

            foreach (var control in _allControls)
            {
                if (control.Layout is LayoutStrings.AXIS)
                {
                    _controlDropdown.options.Add(new(DisambiguateDisplayName(control)));
                }
            }
        }

        // Unsure if this is too hacky/hardcoded
        // Also should really be localized
        private string DisambiguateDisplayName(ControlItemInfo item)
        {
            switch (item.ParentLayout)
            {
                case LayoutStrings.DPAD:
                    return $"D-Pad {item.DisplayName}";
                case LayoutStrings.STICK:
                    return $"Joystick {item.DisplayName}";
            }

            return item.DisplayName;
        }
    }
}
