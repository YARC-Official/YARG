using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

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
            Title.color = MenuData.Colors.BrightText;

            ClearButtons();
        }

        public void ClearButtons()
        {
            _dialogButtonContainer.DestroyChildren();
        }

        public virtual void Submit()
        {
        }

        public void Close()
        {
            OnBeforeClose();
            gameObject.SetActive(false);
        }

        protected virtual void OnBeforeClose()
        {
        }

        public UniTask WaitUntilClosed()
        {
            return UniTask.WaitUntil(() => this == null || !gameObject.activeSelf);
        }
    }
}
