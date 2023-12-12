using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class ButtonBindView : BindView<float, ButtonBinding, SingleButtonBinding>
    {
        [SerializeField]
        private AxisDisplay _rawValueDisplay;
        [SerializeField]
        private Image _rawPressedIndicator;
        [SerializeField]
        private AxisDisplay _calibratedValueDisplay;
        [SerializeField]
        private Image _calibratedPressedIndicator;

        [Space]
        [SerializeField]
        private Color _pressedColor;
        [SerializeField]
        private Color _releasedColor;

        [Space]
        [SerializeField]
        private Toggle _invertToggle;
        [SerializeField]
        private ValueSlider _pressPointSlider;
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(EditProfileMenu editProfileMenu, ButtonBinding binding, SingleButtonBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

            // Set with notify for propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _pressPointSlider.Value = singleBinding.PressPoint;
            _debounceSlider.Value = singleBinding.DebounceThreshold;

            singleBinding.StateChanged += OnStateChanged;
            OnStateChanged(singleBinding.State);
        }

        private void OnDestroy()
        {
            SingleBinding.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(float state)
        {
            _rawValueDisplay.Value = SingleBinding.RawState;
            _calibratedValueDisplay.Value = state;

            _rawPressedIndicator.color = SingleBinding.IsPressedRaw ? _pressedColor : _releasedColor;
            _calibratedPressedIndicator.color = SingleBinding.IsPressed ? _pressedColor : _releasedColor;
        }

        public void OnInvertChanged(bool value)
        {
            SingleBinding.Inverted = value;
        }

        public void OnPressPointChanged(float value)
        {
            SingleBinding.PressPoint = value;
            _rawValueDisplay.PressPoint = value;
            _calibratedValueDisplay.PressPoint = value;
        }

        public void OnDebounceValueChanged(float value)
        {
            SingleBinding.DebounceThreshold = (long) value;
        }
    }
}