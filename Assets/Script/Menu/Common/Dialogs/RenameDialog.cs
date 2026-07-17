using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Dialogs
{
    // pattern: Imperative Shell

    public class RenameDialog : Dialog
    {
        [SerializeField]
        private TMP_InputField _inputField;

        public Action<string> RenameAction;

        private void Awake()
        {
            _inputField.onEndEdit.AddListener(_ =>
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null &&
                    !eventSystem.alreadySelecting &&
                    eventSystem.currentSelectedGameObject == _inputField.gameObject)
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            });
        }

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
