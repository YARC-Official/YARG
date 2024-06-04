using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Dialogs
{
    public class ListToggleSetting : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;
        [SerializeField]
        private Toggle _toggle;

        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        public bool Toggled
        {
            get => _toggle.isOn;
            set => _toggle.isOn = value;
        }

        public Toggle.ToggleEvent OnToggled
        {
            get => _toggle.onValueChanged;
            set => _toggle.onValueChanged = value;
        }
    }
}