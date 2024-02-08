using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class SingleButtonBindView : SingleBindView<float, ButtonBinding, SingleButtonBinding>
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

        public override void Init(ButtonBinding binding, SingleButtonBinding singleBinding)
        {
            base.Init(binding, singleBinding);

            // Set with notify for propogation to other components
            _invertToggle.isOn = singleBinding.Inverted;
            _pressPointSlider.Value = singleBinding.PressPoint;
            _debounceModeDropdown.value = (int) singleBinding.DebounceMode;
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
            _valueDisplay.Value = state;
            _pressedIndicator.IsPressed = SingleBinding.IsPressed;
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
    }
}