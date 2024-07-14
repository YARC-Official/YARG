using System;
using TMPro;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Persistent;

namespace YARG.Menu.Dialogs
{
    public class ConfirmDeleteDialog : MessageDialog
    {
        [SerializeField]
        private TMP_InputField _inputField;
        [SerializeField]
        private TextMeshProUGUI _inputFieldPlaceholder;

        public Action DeleteAction;

        private string _confirmText;

        public void InitializeWithConfirmText(string confirmText, string additionalMessageText)
        {
            _confirmText = confirmText;

            Title.text = Localize.KeyFormat("Menu.Dialog.ConfirmDelete.Title", confirmText);
            Message.text = Localize.KeyFormat("Menu.Dialog.ConfirmDelete.Message", confirmText, additionalMessageText);
            _inputFieldPlaceholder.text = Localize.KeyFormat("Menu.Dialog.ConfirmDelete.Confirm", confirmText);
        }

        public override void Submit()
        {
            if (_inputField.text != _confirmText) return;

            DeleteAction?.Invoke();

            // Close the dialog and invoke the delete
            // action if the user entered the correct text.
            DialogManager.Instance.ClearDialog();
        }
    }
}