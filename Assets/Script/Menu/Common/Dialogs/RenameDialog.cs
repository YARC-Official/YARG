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
        public bool AllowEmpty { get; set; }

        public override void Submit()
        {
            if (string.IsNullOrEmpty(_inputField.text) && !AllowEmpty) return;

            RenameAction?.Invoke(_inputField.text);
        }

        public void ActivateInputField()
        {
            _inputField.ActivateInputField();
        }

        public void SetInitialText(string text, bool selectAll = true)
        {
            _inputField.text = text ?? string.Empty;

            if (!selectAll)
            {
                _inputField.caretPosition = _inputField.text.Length;
                return;
            }

            _inputField.selectionAnchorPosition = 0;
            _inputField.selectionFocusPosition = _inputField.text.Length;
            _inputField.caretPosition = _inputField.text.Length;
        }
    }
}