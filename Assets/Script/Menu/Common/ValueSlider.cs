using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace YARG.Menu
{
    [ExecuteAlways]
    public class ValueSlider : MonoBehaviour
    {
        [SerializeField]
        private Slider _slider;
        [SerializeField]
        private TMP_InputField _inputField;
        [SerializeField]
        private string _formatString = "N2";

        [Space]
        public UnityEvent<float> ValueChanged;

        public bool ClampMin = true;
        public bool ClampMax = true;

        private float _value = 0f;
        public float Value
        {
            get => _value;
            set => SetValue(value, notify: true);
        }

        public float MinimumValue
        {
            get => _slider.minValue;
            set => _slider.minValue = value;
        }

        public float MaximumValue
        {
            get => _slider.maxValue;
            set => _slider.maxValue = value;
        }

        private string _beforeEditText;
        private float _beforeEditValue;

        // TMP_InputField.SetTextWithoutNotify still notifies when in editor
        private bool _isSettingValue;

        public void OnSelect()
        {
            if (_isSettingValue)
                return;

            _beforeEditText = _inputField.text;
            _beforeEditValue = _value;
        }

        public void OnDeselect()
        {
            if (_isSettingValue)
                return;

            _beforeEditText = null;
        }

        public void OnSliderChange(float value)
        {
            // Ignore if no change
            if (Mathf.Approximately(_value, value))
                return;

            SetValue(value, setSlider: false, notify: true);
        }

        public void OnTextChanged(string text)
        {
            if (_isSettingValue || !float.TryParse(text, out float value))
                return;

            // Text/value are not updated until the edit is finished,
            // and notification is delayed
            SetValue(value, setValue: false, setText: false, notify: false);
        }

        public void OnFinishEdit(string text)
        {
            if (_isSettingValue)
                return;

            // Reset text/value on invalid input
            if (!float.TryParse(text, out float value))
            {
                _isSettingValue = true;

                _inputField.SetTextWithoutNotify(_beforeEditText);
                _slider.SetValueWithoutNotify(_beforeEditValue);

                _isSettingValue = false;
                return;
            }

            SetValue(value, notify: true);
        }

        private float ClampValue(float value)
        {
            if (ClampMin)
                value = Math.Max(value, _slider.minValue);

            if (ClampMax)
                value = Math.Min(value, _slider.maxValue);

            if (_slider.wholeNumbers)
                value = Mathf.Round(value);

            return value;
        }

        private void SetValue(float value, bool notify,
            bool setSlider = true, bool setValue = true, bool setText = true)
        {
            if (_isSettingValue)
                return;

            // Set value and notify
            _isSettingValue = true;

            value = ClampValue(value);

            if (setSlider)
                _slider.SetValueWithoutNotify(value);

            if (setValue)
                _value = value;

            if (setText)
                _inputField.SetTextWithoutNotify(value.ToString(_formatString));

            if (notify)
                ValueChanged.Invoke(value);

            _isSettingValue = false;
        }

        public void SetValueWithoutNotify(float value)
        {
            SetValue(value, notify: false);
        }
    }
}