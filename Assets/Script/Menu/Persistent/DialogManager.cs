using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;

namespace YARG.Menu.Persistent
{
    public class DialogManager : MonoSingleton<DialogManager>
    {
        [SerializeField]
        private Transform _dialogContainer;

        [Space]
        [SerializeField]
        private MessageDialog _messagePrefab;
        [SerializeField]
        private OneTimeMessageDialog _oneTimeMessagePrefab;
        [SerializeField]
        private ListDialog _listPrefab;
        [SerializeField]
        private ListWithSettingsDialog _listWithSettingsPrefab;
        [SerializeField]
        private RenameDialog _renameDialog;
        [SerializeField]
        private ConfirmDeleteDialog _confirmDeleteDialog;
        [SerializeField]
        private ColorPickerDialog _colorPickerDialog;

        private Dialog _currentDialog;

        public bool IsDialogShowing => _currentDialog != null;

        // <inheritdoc> doesn't respect type parameters correctly

        /// <summary>
        /// Displays and returns a message dialog.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{MessageDialog}(MessageDialog)"/>
        public MessageDialog ShowMessage(string title, string message)
        {
            var dialog = ShowDialog(_messagePrefab);

            dialog.Title.text = title;
            dialog.Message.text = message;

            return dialog;
        }

        /// <summary>
        /// Displays and returns a one time message dialog. If the "dont show again" toggle is checked,
        /// <paramref name="dontShowAgainAction"/> will be invoked.
        /// </summary>
        /// <remarks>
        /// The given localization key must have <c>Title</c>, <c>Description</c>, and <c>Confirm</c> children.
        /// </remarks>
        /// <inheritdoc cref="ShowDialog{MessageDialog}(MessageDialog)"/>
        public OneTimeMessageDialog ShowOneTimeMessage(string localizationKey, Action dontShowAgainAction)
        {
            var dialog = ShowDialog(_oneTimeMessagePrefab);

            dialog.Title.text = Localize.Key(localizationKey, "Title");
            dialog.Message.text = Localize.Key(localizationKey, "Description");

            dialog.DontShowAgainAction = dontShowAgainAction;

            dialog.ClearButtons();
            dialog.AddDialogButton(
                Localize.MakeKey(localizationKey, "Confirm"),
                MenuData.Colors.ConfirmButton,
                ClearDialog
            );

            return dialog;
        }

        /// <summary>
        /// Displays and returns a list dialog.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{ListDialog}(ListDialog)"/>
        public ListDialog ShowList(string title)
        {
            var dialog = ShowDialog(_listPrefab);

            dialog.Title.text = title;

            return dialog;
        }

        /// <summary>
        /// Displays and returns a list dialog with configurable settings.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{ListWithSettingsDialog}(ListWithSettingsDialog)"/>
        public ListWithSettingsDialog ShowListWithSettings(string title)
        {
            var dialog = ShowDialog(_listWithSettingsPrefab);

            dialog.Title.text = title;

            return dialog;
        }

        /// <summary>
        /// Displays and returns a rename dialog.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{ListDialog}(ListDialog)"/>
        public RenameDialog ShowRenameDialog(string title, Action<string> renameAction)
        {
            var dialog = ShowDialog(_renameDialog);

            dialog.Title.text = title;

            dialog.RenameAction = renameAction;

            dialog.ClearButtons();
            dialog.AddDialogButton("Menu.Common.Cancel", MenuData.Colors.CancelButton, ClearDialog);
            dialog.AddDialogButton("Menu.Common.Confirm", MenuData.Colors.ConfirmButton, SubmitAndClearDialog);

            // Make the dialog input field active so no mouse is required
            dialog.ActivateInputField();

            return dialog;
        }

        /// <summary>
        /// Displays and returns a confirm delete dialog.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{ListDialog}(ListDialog)"/>
        public ConfirmDeleteDialog ShowConfirmDeleteDialog(string additionalMessageText,
            Action deleteAction, string confirmText)
        {
            var dialog = ShowDialog(_confirmDeleteDialog);

            dialog.InitializeWithConfirmText(confirmText, additionalMessageText);
            dialog.DeleteAction = deleteAction;

            dialog.ClearButtons();
            dialog.AddDialogButton("Menu.Common.Cancel", MenuData.Colors.BrightButton, ClearDialog);
            dialog.AddDialogButton("Menu.Common.Delete", MenuData.Colors.CancelButton, () => _currentDialog.Submit());

            return dialog;
        }

        /// <summary>
        /// Displays and returns a confirm delete dialog.
        /// </summary>
        /// <inheritdoc cref="ShowDialog{ListDialog}(ListDialog)"/>
        public ColorPickerDialog ShowColorPickerDialog(Color initialColor, Action<Color> colorPickAction)
        {
            var dialog = ShowDialog(_colorPickerDialog);

            dialog.Title.text = Localize.Key("Menu.Dialog.ColorPicker.Title");
            dialog.Initialize(initialColor);
            dialog.ColorPickAction = colorPickAction;

            dialog.ClearButtons();
            dialog.AddDialogButton("Menu.Common.Cancel", MenuData.Colors.CancelButton, ClearDialog);
            dialog.AddDialogButton("Menu.Common.Apply", MenuData.Colors.ConfirmButton, () => _currentDialog.Submit());

            return dialog;
        }

        /// <summary>
        /// Displays and returns a <typeparamref name="TDialog"/>.
        /// </summary>
        /// <remarks>
        /// Do not hold on to the returned dialog! It will be destroyed when closed by the user.
        /// </remarks>
        public TDialog ShowDialog<TDialog>(TDialog prefab)
            where TDialog : Dialog
        {
            if (IsDialogShowing)
                throw new InvalidOperationException("A dialog already exists! Clear the previous dialog before showing a new one.");

            var dialog = Instantiate(prefab, _dialogContainer);
            _currentDialog = dialog;

            dialog.ClearButtons();
            dialog.AddDialogButton("Menu.Common.Close", MenuData.Colors.CancelButton, ClearDialog);

            return dialog;
        }

        /// <summary>
        /// Destroys the currently-displayed dialog.
        /// </summary>
        /// <remarks>
        /// By default, this is called automatically when the user hits the Close button.
        /// If setting custom buttons, be sure to hook this method up to one, or that
        /// you call it manually after a desired condition is met.
        /// </remarks>
        public void ClearDialog()
        {
            if (_currentDialog == null) return;

            _currentDialog.Close();
            Destroy(_currentDialog.gameObject);
            _currentDialog = null;
        }

        /// <summary>
        /// Submits then clears the dialog using <see cref="ClearDialog"/>
        /// </summary>
        public void SubmitAndClearDialog()
        {
            _currentDialog.Submit();
            ClearDialog();
        }

        /// <summary>
        /// Destroys the currently-displayed dialog.
        /// </summary>
        public UniTask WaitUntilCurrentClosed()
        {
            if (_currentDialog == null)
                return UniTask.CompletedTask;

            return _currentDialog.WaitUntilClosed();
        }
    }
}