using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Dialogs
{
    public abstract class Dialog : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _title;

        [Space]
        [SerializeField]
        private Transform _dialogButtonContainer;
        [SerializeField]
        private ColoredButton _dialogButtonPrefab;

        public TextMeshProUGUI Title => _title;

        public ColoredButton AddDialogButton(string text, UnityAction action)
        {
            var button = Instantiate(_dialogButtonPrefab, _dialogButtonContainer);

            button.Text.text = text;
            button.OnClick.AddListener(action);

            return button;
        }

        public ColoredButton AddDialogButton(string text, Color backgroundColor, UnityAction action)
        {
            var button = AddDialogButton(text, action);

            button.SetBackgroundAndTextColor(backgroundColor);

            return button;
        }

        public ColoredButton AddDialogButton(string text, Color backgroundColor, Color textColor, UnityAction action)
        {
            var button = AddDialogButton(text, action);

            button.BackgroundColor = backgroundColor;
            button.Text.color = textColor;

            return button;
        }

        public void ClearButtons()
        {
            _dialogButtonContainer.DestroyChildren();
        }
    }
}
