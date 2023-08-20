using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Dialogs
{
    public abstract class Dialog : MonoBehaviour
    {
        [SerializeField]
        private Transform _dialogButtonContainer;
        [SerializeField]
        private ColoredButton _dialogButtonPrefab;

        [field: Space]
        [field: SerializeField]
        public TextMeshProUGUI Title { get; private set; }

        protected void Initialize(string title)
        {
            Title.text = title;
        }

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

        public virtual void ClearDialog()
        {
            Title.text = null;
            Title.color = ColoredButton.BrightTextColor;

            ClearButtons();
        }

        public void ClearButtons()
        {
            _dialogButtonContainer.DestroyChildren();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
