// pattern: Imperative Shell

using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Navigation;

namespace YARG.Menu.Dialogs
{
    public abstract class Dialog : MonoBehaviour
    {
        [SerializeField]
        private Transform _dialogButtonContainer;
        [SerializeField]
        private NavigationGroup _navigationGroup;
        [SerializeField]
        private ColoredButton _dialogButtonPrefab;

        [field: Space]
        [field: SerializeField]
        public TextMeshProUGUI Title { get; private set; }

        protected NavigationGroup NavigationGroup => _navigationGroup;
        protected Transform DialogButtonContainer => _dialogButtonContainer;

        private void OnEnable()
        {
            // The dialog owns this scheme and must register it before input can
            // reach the underlying menu.
            Navigator.Instance.PushSchemeImmediate(GetNavigationScheme());
        }

        protected virtual NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => NavigationGroup.SelectPrevious(ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => NavigationGroup.SelectNext(ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => NavigationGroup.ConfirmSelection()),
            }, null);
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public virtual void Initialize()
        {

        }

        public ColoredButton AddDialogButton(string localizeKey, UnityAction action)
        {
            var button = Instantiate(_dialogButtonPrefab, _dialogButtonContainer);

            // Add the navigatable button, and select it
            var nav = button.GetComponentInChildren<NavigatableUnityButton>();
            if (nav != null)
            {
                _navigationGroup.AddNavigatable(nav);
            }

            button.Text.text = Localize.Key(localizeKey);
            button.OnClick.AddListener(action);

            return button;
        }

        public virtual ColoredButton AddDialogButton(string localizeKey, Color backgroundColor, UnityAction action)
        {
            var button = AddDialogButton(localizeKey, action);

            button.SetBackgroundAndTextColor(backgroundColor);

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
            _navigationGroup.ClearNavigatables();
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

        public bool IsOpen => this != null && gameObject.activeSelf;

        public UniTask WaitUntilClosed()
        {
            return UniTask.WaitUntil(() => !IsOpen);
        }
    }
}
