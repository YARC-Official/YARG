using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.ProfileInfo
{
    public class ButtonDisplay : MonoBehaviour
    {
        [SerializeField]
        private Color _pressedColor;
        [SerializeField]
        private Color _releasedColor;

        [Space]
        [SerializeField]
        private Image _pressedIndicator;

        private bool _isPressed;

        public bool IsPressed
        {
            get => _isPressed;
            set
            {
                _isPressed = value;
                _pressedIndicator.color = value ? _pressedColor : _releasedColor;
            }
        }
    }
}