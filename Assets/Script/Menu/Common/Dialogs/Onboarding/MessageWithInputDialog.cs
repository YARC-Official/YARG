using TMPro;
using UnityEngine;
using YARG.Menu.Data;

namespace YARG.Menu.Dialogs
{
    public class MessageWithInputDialog : RenameDialog
    {
        [field: Space]
        [SerializeField]
        public TextMeshProUGUI Message { get; private set; }
        [SerializeField]
        private DiscreteProgressDisplay _progressDisplay;

        public override void ClearDialog()
        {
            base.ClearDialog();

            Message.text = null;
            Message.color = MenuData.Colors.BrightText;
        }
    }
}