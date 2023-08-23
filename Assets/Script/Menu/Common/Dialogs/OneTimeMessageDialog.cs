using System;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Dialogs
{
    public class OneTimeMessageDialog : MessageDialog
    {
        [SerializeField]
        private Toggle _toggle;

        public Action DontShowAgainAction;

        protected override void OnBeforeClose()
        {
            if (_toggle.isOn)
            {
                DontShowAgainAction?.Invoke();
            }
        }
    }
}