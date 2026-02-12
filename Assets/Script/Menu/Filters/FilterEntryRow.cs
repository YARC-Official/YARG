using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Filters
{
    public class FilterEntryRow : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;
        [SerializeField]
        private TMP_Text _number;
        [SerializeField]
        private Toggle _toggle;

        public event Action<bool> ToggleChanged;

        public Toggle Toggle => _toggle;

        private FilterRowBackgroundVisual _backgroundVisual;

        private void Awake()
        {
            _backgroundVisual = GetComponent<FilterRowBackgroundVisual>();
            if (_backgroundVisual == null)
                _backgroundVisual = gameObject.AddComponent<FilterRowBackgroundVisual>();

            if (_toggle != null)
                _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnDestroy()
        {
            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        private void OnToggleValueChanged(bool value)
        {
            ToggleChanged?.Invoke(value);
        }

        public void Bind(string labelText, string numberText, bool isOn)
        {
            SetLabelText(labelText);
            SetNumberText(numberText);
            SetToggleIsOn(isOn);
        }

        public void AssignIndex(int index)
        {
            _backgroundVisual?.AssignIndex(index);
        }

        public void SetLabelText(string text)
        {
            if (_label != null)
                _label.text = text;
        }

        public void SetNumberText(string text)
        {
            if (_number != null)
                _number.text = text;
        }

        public void SetToggleIsOn(bool isOn)
        {
            if (_toggle != null)
                _toggle.SetIsOnWithoutNotify(isOn);
        }
    }
}
