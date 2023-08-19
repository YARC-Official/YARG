using TMPro;
using UnityEngine;

namespace YARG.Menu.Dialogs
{
    public class MessageDialog : Dialog
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _message;

        public TextMeshProUGUI Message => _message;
    }
}
