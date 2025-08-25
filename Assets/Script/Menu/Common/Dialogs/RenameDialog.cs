using System;
using TMPro;
using UnityEngine;

namespace YARG.Menu.Dialogs
{
    public class RenameDialog : Dialog
    {
        [SerializeField]
        private TMP_InputField _inputField;

        public Action<string> RenameAction;

        public override void Submit()
        {
            if (string.IsNullOrEmpty(_inputField.text)) return;

            RenameAction?.Invoke(_inputField.text);
        }

        public void ActivateInputField()
        {
            _inputField.ActivateInputField();
        }
    }
}