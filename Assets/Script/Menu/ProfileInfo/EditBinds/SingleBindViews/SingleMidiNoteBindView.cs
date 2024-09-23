using System;
using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class SingleMidiNoteBindView : SingleBindView<float, ButtonBinding, SingleButtonBinding>
    {
        private const float MAX_VELOCITY = 127;

        [SerializeField]
        private AxisDisplay _valueDisplay;
        [SerializeField]
        private ButtonDisplay _pressedIndicator;
        [SerializeField]
        private TMP_InputField _velocityText;

        [Space]
        [SerializeField]
        private ValueSlider _velocityThresholdSlider;
        [SerializeField]
        private TMP_Dropdown _debounceModeDropdown;
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(ButtonBinding binding, SingleButtonBinding singleBinding)
        {
            base.Init(binding, singleBinding);

            // Set with notify for propogation to other components
            _velocityThresholdSlider.SetValueWithoutNotify(MAX_VELOCITY); // Ensure change event is fired, this starts at 1
            _velocityThresholdSlider.Value = (int) Math.Round(singleBinding.PressPoint * MAX_VELOCITY);
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
            _valueDisplay.Value = -1f + (state * 2f);
            _pressedIndicator.IsPressed = SingleBinding.IsPressed;

            // Only update velocity display on note ons
            int velocity = (int) (state * MAX_VELOCITY);
            if (velocity > 0)
                _velocityText.text = velocity.ToString();
        }

        public void OnVelocityThresholdChanged(float value)
        {
            float pressPoint = value / MAX_VELOCITY;
            SingleBinding.PressPoint = pressPoint;
            _valueDisplay.PressPoint = -1f + (pressPoint * 2f);
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