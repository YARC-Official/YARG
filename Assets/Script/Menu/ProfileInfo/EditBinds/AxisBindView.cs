using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class AxisBindView : BindView<float, AxisBinding, SingleAxisBinding>
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

        public override void Init(EditProfileMenu editProfileMenu, AxisBinding binding, SingleAxisBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

            // Set with notify for value corrections and propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _maxValueSlider.Value = singleBinding.Maximum;
            _minValueSlider.Value = singleBinding.Minimum;
            _upperDeadzoneSlider.Value = singleBinding.UpperDeadzone;
            _lowerDeadzoneSlider.Value = singleBinding.LowerDeadzone;

            singleBinding.StateChanged += OnStateChanged;
            OnStateChanged(singleBinding.State);
        }

        private void OnDestroy()
        {
            SingleBinding.StateChanged -= OnStateChanged;
        }

        public void OnInvertChanged(bool value)
        {
            SingleBinding.Inverted = value;
        }

        private void OnStateChanged(float state)
        {
            _rawValueDisplay.Value = SingleBinding.RawState;
            _calibratedValueDisplay.Value = state;
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
    }
}