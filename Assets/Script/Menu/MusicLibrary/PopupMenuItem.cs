using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Menu.Navigation;

namespace YARG.Menu.MusicLibrary
{
    public class PopupMenuItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _body;

        [field: SerializeField]
        public NavigatableButton Button { get; private set; }

        public void Initialize(string body, UnityAction action)
        {
            _body.text = body;
            Button.SetOnClickEvent(action);
        }
    }
}