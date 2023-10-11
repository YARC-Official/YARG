using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class InputViewerButton : MonoBehaviour
    {
        private const int TIMER_BUFFER_SIZE = 9;

        private const float DISABLED_ALPHA = 0.1f;

        [SerializeField]
        private TextMeshProUGUI _inputTimeText;

        [SerializeField]
        private TextMeshProUGUI _holdTimeText;

        [SerializeField]
        private Image _imageHighlight;

        public Color ButtonColor { get; private set; }

        private char[] _inputTimeBuffer;
        private char[] _holdTimeBuffer;

        private double _inputTime;
        private double _holdTime;

        private bool _isPressed;

        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _inputTimeBuffer = new char[TIMER_BUFFER_SIZE];
            _holdTimeBuffer = new char[TIMER_BUFFER_SIZE];
        }

        public void UpdatePressState(bool pressed, double time)
        {
            _inputTime = time;
            _holdTime = 0;

            _isPressed = pressed;

            SetTextBuffer(_inputTimeBuffer, _inputTime);
            SetTextBuffer(_holdTimeBuffer, _holdTime);

            _inputTimeText.SetCharArray(_inputTimeBuffer, 0, _inputTimeBuffer.Length);
            _holdTimeText.SetCharArray(_holdTimeBuffer, 0, _holdTimeBuffer.Length);

            var color = ButtonColor;
            color.a = pressed ? 1 : DISABLED_ALPHA;

            _imageHighlight.color = color;
        }

        private void Update()
        {
            if (_isPressed)
            {
                _holdTime = _gameManager.InputTime - _inputTime;

                SetTextBuffer(_holdTimeBuffer, _holdTime);
            }
        }

        private static void SetTextBuffer(char[] buffer, double time)
        {
            // Clear buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = '\0';
            }

            int whole = (int) time;
            int fraction = (int) ((time - whole) * 1000);

            // Write whole number (max 5 digits, equivalent to 27.7 hours)
            int index = 4;
            while (whole > 0 && index >= 0)
            {
                buffer[index--] = (char) ('0' + (whole % 10));
                whole /= 10;
            }

            index = 5;

            // Write decimal point
            buffer[index++] = '.';

            // Write fraction
            buffer[index++] = (char) ('0' + (fraction / 100));

            fraction %= 100;
            buffer[index++] = (char) ('0' + (fraction / 10));

            fraction %= 10;
            buffer[index] = (char) ('0' + fraction);
        }
    }
}