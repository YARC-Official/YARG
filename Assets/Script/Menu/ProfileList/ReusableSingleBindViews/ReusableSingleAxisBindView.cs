using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleAxisBindView : ReusableSingleBindView<ReusableAxisBinding, ReusableSingleAxisBinding, float>
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

        public override void Init(ReusableAxisBinding binding, ReusableSingleAxisBinding singleBinding, List<ControlItemInfo> controls, ProfilesMenu profilesMenu)
        {
            base.Init(binding, singleBinding, controls, profilesMenu);

            // Set with notify for value corrections and propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _maxValueSlider.Value = singleBinding.Maximum;
            _minValueSlider.Value = singleBinding.Minimum;
            _upperDeadzoneSlider.Value = singleBinding.UpperDeadzone;
            _lowerDeadzoneSlider.Value = singleBinding.LowerDeadzone;
        }

        public void OnInvertChanged()
        {
            SingleBinding.Inverted = _invertToggle.isOn;
        }

        public void OnMaxValueChanged()
        {
            var value = _maxValueSlider.Value;

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

        public void OnMinValueChanged()
        {
            var value = _minValueSlider.Value;

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

        public void OnUpperDeadzoneChanged()
        {
            var value = _upperDeadzoneSlider.Value;

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

        public void OnLowerDeadzoneChanged()
        {
            var value = _lowerDeadzoneSlider.Value;

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

            // Organize available controls by how likely they are to be relevant to an axis binding; we don't want to frontload
            // a bunch of buttons when the player is more likely to want a pitchwheel, accelerometer, CC dial, etc.
            var midiPitchControls = new List<ControlItemInfo>();
            var axisControls = new List<ControlItemInfo>();
            var midiValueControls = new List<ControlItemInfo>();
            var integerControls = new List<ControlItemInfo>();
            var buttonControls = new List<ControlItemInfo>();
            var keyControls = new List<ControlItemInfo>();
            var otherControls = new List<ControlItemInfo>();


            foreach (var control in _allControls)
            {
                var relevantList = control.Layout switch
                {
                    LayoutStrings.MIDI_PITCH => midiPitchControls,
                    LayoutStrings.AXIS => axisControls,
                    LayoutStrings.MIDI_VALUE => midiValueControls,
                    LayoutStrings.INTEGER => integerControls,
                    LayoutStrings.BUTTON => buttonControls,
                    LayoutStrings.KEY => keyControls,
                    _ => otherControls
                };

                relevantList.Add(control);
            }

            _dropdownControls = midiPitchControls
                .Concat(axisControls)
                .Concat(midiValueControls)
                .Concat(integerControls)
                .Concat(buttonControls)
                .Concat(keyControls)
                .Concat(otherControls)
                .ToList();

            foreach (var control in _dropdownControls)
            {
                _controlDropdown.options.Add(new(DisambiguateDisplayName(control)));
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
