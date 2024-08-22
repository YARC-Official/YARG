using UnityEngine;

namespace YARG
{
    public class ListHeader : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TextMeshProUGUI _textComponent;

        public string Text
        {
            get => _textComponent.text;
            set => _textComponent.text = value;
        }
    }
}
