using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.Profiles
{
    public class ButtonBindView : BindView<float, ButtonBinding, SingleButtonBinding>
    {
        [SerializeField]
        private Slider _rawValueSlider;
        [SerializeField]
        private Image _rawPressedIndicator;
        [SerializeField]
        private Slider _calibratedValueSlider;
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

            _invertToggle.SetIsOnWithoutNotify(singleBinding.Inverted);
            _pressPointSlider.SetValueWithoutNotify(singleBinding.PressPoint);
            _debounceSlider.SetValueWithoutNotify(singleBinding.DebounceThreshold);

            singleBinding.StateChanged += OnStateChanged;
            OnStateChanged(singleBinding.State);
        }

        private void OnDestroy()
        {
            SingleBinding.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(float state)
        {
            _rawValueSlider.value = SingleBinding.RawState;
            _calibratedValueSlider.value = state;

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
        }

        public void OnDebounceValueChanged(float value)
        {
            SingleBinding.DebounceThreshold = (long) value;
        }
    }
}