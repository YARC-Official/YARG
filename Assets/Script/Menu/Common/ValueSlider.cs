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

        [Space]
        public UnityEvent<float> ValueChanged;

        public bool NotifyOnChange { get; set; }

        private float _value = 0f;
        public float Value
        {
            get => _value;
            set => SetValue(value);
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

        // TMP_InputField.SetTextWithoutNotify still notifies when in editor
        private bool _isSettingValue;

        public void OnSliderChange(float value)
        {
            SetValue(value);
        }

        public void OnTextChange(string text)
        {
            if (!float.TryParse(text, out float value))
                return;

            SetValue(value);
        }

        private void SetValue(float value)
        {
            if (_isSettingValue)
                return;

            // Ignore if no change
            if (Mathf.Approximately(_value, value))
                return;

            // Set value and notify
            _isSettingValue = true;

            _slider.SetValueWithoutNotify(value);
            _value = _slider.value;
            _inputField.SetTextWithoutNotify(_value.ToString("N2"));

            if (NotifyOnChange) ValueChanged.Invoke(_value);

            _isSettingValue = false;
        }

        public void SetValueWithoutNotify(float value)
        {
            bool notify = NotifyOnChange;
            NotifyOnChange = false;
            SetValue(value);
            NotifyOnChange = notify;
        }
    }
}