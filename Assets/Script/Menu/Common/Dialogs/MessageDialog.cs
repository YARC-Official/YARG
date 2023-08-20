using TMPro;
using UnityEngine;

namespace YARG.Menu.Dialogs
{
    public class MessageDialog : Dialog
    {
        [field: Space]
        [field: SerializeField]
        public TextMeshProUGUI Message { get; private set; }

        public override void ClearDialog()
        {
            base.ClearDialog();

            Message.text = null;
            Message.color = ColoredButton.BrightTextColor;
        }
    }
}
