using System;
using TMPro;
using UnityEngine;
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

            Title.text = $"Delete \"{confirmText}\"?";
            Message.text = $"Are you sure you want to delete <b>{confirmText}</b>?\n\n{additionalMessageText}";
            _inputFieldPlaceholder.text = $"Type <b>{confirmText}</b> here to confirm";
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