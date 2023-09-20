using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class AxisBindView : BindView<float, AxisBinding, SingleAxisBinding>
    {
        [SerializeField]
        private Slider _rawValueSlider;
        [SerializeField]
        private Slider _calibratedValueSlider;

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

            _invertToggle.SetIsOnWithoutNotify(singleBinding.Inverted);

            // Set with notify so that value corrections will occur
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
            _rawValueSlider.value = SingleBinding.RawState;
            _calibratedValueSlider.value = state;
        }

        public void OnMaxValueChanged(float value)
        {
            SingleBinding.Maximum = value;

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

            if (value < SingleBinding.Minimum)
                _minValueSlider.Value = value;

            if (value > SingleBinding.Maximum)
                _maxValueSlider.Value = value;

            if (value > SingleBinding.UpperDeadzone)
                _upperDeadzoneSlider.Value = value;
        }
    }
}