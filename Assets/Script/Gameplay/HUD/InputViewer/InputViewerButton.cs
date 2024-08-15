using System;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

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
            _inputTimeText.SetText(Math.Round(_inputTime, 3));
            _holdTimeText.SetText(Math.Round(_inputTime, 3));
            _pressCountText.SetText(_pressCount);

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

                _holdTimeText.SetText(Math.Round(_holdTime, 3));
            }
        }
    }
}