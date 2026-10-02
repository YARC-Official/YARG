using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Settings
{
    public class CareerHeader : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;
        [Space]
        [SerializeField]
        private Button _addButton;
        [SerializeField]
        private Button _editButton;
        [Space]
        [SerializeField]
        private Button _upButton;
        [SerializeField]
        private Button _downButton;
        [SerializeField]
        private Button _removeButton;

        private Action _onAdd;
        private Action _onEdit;
        private Action _onUp;
        private Action _onDown;
        private Action _onRemove;

        public void Initialize(string text, Action onAdd, Action onEdit, Action onUp, Action onDown, Action onRemove)
        {
            if (_text != null)
            {
                _text.text = text;
            }

            _onAdd = onAdd;
            _onEdit = onEdit;
            _onUp = onUp;
            _onDown = onDown;
            _onRemove = onRemove;

            SetupButton(_addButton, _onAdd);
            SetupButton(_editButton, _onEdit);
            SetupButton(_upButton, _onUp);
            SetupButton(_downButton, _onDown);
            SetupButton(_removeButton, _onRemove);
        }

        private static void SetupButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();

            if (action != null)
            {
                button.interactable = true;
                button.onClick.AddListener(() => action());
            }
            else
            {
                button.interactable = false;
                if (button.TryGetComponent<Image>(out var buttonImage))
                {
                    buttonImage.color = button.colors.disabledColor;
                }
            }
        }
    }
}