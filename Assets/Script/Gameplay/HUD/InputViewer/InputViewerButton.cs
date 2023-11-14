using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class InputViewerButton : MonoBehaviour
    {
        private const int TIMER_BUFFER_SIZE = 9;
        private const int PRESS_BUFFER_SIZE = 5;

        private const float DISABLED_ALPHA = 0.1f;

        [SerializeField]
        private TextMeshProUGUI _inputTimeText;

        [SerializeField]
        private TextMeshProUGUI _holdTimeText;

        [SerializeField]
        private TextMeshProUGUI _pressCountText;

        [SerializeField]
        private Image _imageHighlight;

        [HideInInspector]
        public Color ButtonColor;

        private char[] _inputTimeBuffer;
        private char[] _holdTimeBuffer;
        private char[] _pressCountBuffer;

        private double _inputTime;
        private double _holdTime;
        private double _holdStartTime;

        private int _pressCount;

        private bool _isPressed;

        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _inputTimeBuffer = new char[TIMER_BUFFER_SIZE];
            _holdTimeBuffer = new char[TIMER_BUFFER_SIZE];

            _pressCountBuffer = new char[PRESS_BUFFER_SIZE];
        }

        public void UpdatePressState(bool pressed, double time)
        {
            // We don't want to display negative times at the start of the song because to the user it makes no sense
            // Add on the start delay so it offsets back to 0
            time += GameManager.SONG_START_DELAY;

            _inputTime = time;

            // Use the game manager's time instead of the input's time, as video calibration
            // affects the player's input time independently of the game manager's input time
            _holdStartTime = _gameManager.InputTime + GameManager.SONG_START_DELAY;

            _isPressed = pressed;

            if (pressed)
            {
                _pressCount++;
            }

            UpdateVisual();
        }

        public void UpdateVisual()
        {
            WriteDoubleToBuffer(_inputTimeBuffer, _inputTime);
            WriteDoubleToBuffer(_holdTimeBuffer, _holdTime);
            WriteIntToBuffer(_pressCountBuffer, _pressCount);

            _inputTimeText.SetCharArray(_inputTimeBuffer, 0, _inputTimeBuffer.Length);
            _holdTimeText.SetCharArray(_holdTimeBuffer, 0, _holdTimeBuffer.Length);
            _pressCountText.SetCharArray(_pressCountBuffer, 0, _pressCountBuffer.Length);

            var color = ButtonColor;
            color.a = _isPressed ? 0.8f : DISABLED_ALPHA;

            _imageHighlight.color = color;
        }

        public void ResetState()
        {
            _inputTime = 0;
            _holdTime = 0;
            _holdStartTime = 0;
            _pressCount = 0;
            _isPressed = false;

            UpdateVisual();
        }

        private void Update()
        {
            if (_isPressed)
            {
                _holdTime = _gameManager.InputTime - _holdStartTime + GameManager.SONG_START_DELAY;

                WriteDoubleToBuffer(_holdTimeBuffer, _holdTime);
                _holdTimeText.SetCharArray(_holdTimeBuffer, 0, _holdTimeBuffer.Length);
            }
        }

        private static void WriteDoubleToBuffer(char[] buffer, double num)
        {
            const int fractionLength = 3;

            if (num == 0)
            {
                buffer[0] = '0';
                buffer[1] = '.';
                buffer[2] = '0';
                buffer[3] = '0';
                buffer[4] = '0';
            }

            // Clear buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = '\0';
            }

            int whole = (int) num;
            int fraction = (int) ((num - whole) * 1000);

            // -1 for decimal point
            int integerLength = buffer.Length - fractionLength - 1;

            // Write whole number
            int wholeEndIndex = integerLength - 1;
            int index = wholeEndIndex;

            if (whole == 0)
            {
                buffer[wholeEndIndex] = '0';
            }

            while (whole > 0 && index >= 0)
            {
                buffer[index--] = (char) ('0' + (whole % 10));
                whole /= 10;
            }

            index = integerLength;

            // Write decimal point
            buffer[index++] = '.';

            // Write fraction
            buffer[index++] = (char) ('0' + (fraction / 100));

            fraction %= 100;
            buffer[index++] = (char) ('0' + (fraction / 10));

            fraction %= 10;
            buffer[index] = (char) ('0' + fraction);

            TrimNullStart(buffer);
        }

        private static void WriteIntToBuffer(char[] buffer, int num)
        {
            // Clear buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = '\0';
            }

            if (num == 0)
            {
                buffer[0] = '0';
                return;
            }

            int index = buffer.Length - 1;
            while (num > 0 && index >= 0)
            {
                buffer[index--] = (char) ('0' + (num % 10));
                num /= 10;
            }

            TrimNullStart(buffer);
        }

        private static void TrimNullStart(char[] buffer)
        {
            bool nonNullFound = false;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != '\0')
                {
                    nonNullFound = true;
                    break;
                }
            }

            if (!nonNullFound)
            {
                return;
            }

            while (buffer[0] == '\0')
            {
                for (int i = 0; i < buffer.Length - 1; i++)
                {
                    buffer[i] = buffer[i + 1];
                }

                buffer[^1] = '\0';
            }
        }
    }
}