using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.Persistent
{
    public class IdleDimmer : MonoSingleton<IdleDimmer>
    {
        private const float IDLE_DELAY_SECONDS = 60f * 3; // 3 minutes
        private const float DIM_ALPHA = 0.8f; // 80% opaque
        private const float RAMP_DURATION_SECONDS = 20f;

        [SerializeField]
        private Image _dimmer;

        private float _lastActivityTime;

        protected override void SingletonAwake()
        {
            ResetTimer();
            SetDimmed(0f);
        }

        private void OnEnable()
        {
            InputManager.MenuInput += OnMenuInput;
        }

        private void OnDisable()
        {
            InputManager.MenuInput -= OnMenuInput;
        }

        private void Update()
        {
            var currentScene = GlobalVariables.Instance.CurrentScene;
            bool isGameplay = currentScene is SceneIndex.Gameplay;
            bool isNotFocused = !Application.isFocused;
            bool didReceiveInput = CheckKeyboardMouse();

            if (didReceiveInput || isGameplay || isNotFocused)
            {
                ResetTimer();
            }

            var idleDuration = Time.unscaledTime - _lastActivityTime;
            var rampProgress = Mathf.Clamp01((idleDuration - IDLE_DELAY_SECONDS) / RAMP_DURATION_SECONDS);
            SetDimmed(rampProgress);
        }

        private void OnMenuInput(YargPlayer player, ref GameInput input)
        {
            ResetTimer();
        }

        private bool CheckKeyboardMouse()
        {
            bool hasKeyboardActivity = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool hasMouseActivity = HasMouseActivity();
            return hasKeyboardActivity || hasMouseActivity;
        }

        private static bool HasMouseActivity()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            bool hasMouseMovement = mouse.delta.ReadValue() != Vector2.zero;
            bool hasMouseButtonActivity =
                mouse.leftButton.wasPressedThisFrame ||
                mouse.rightButton.wasPressedThisFrame ||
                mouse.middleButton.wasPressedThisFrame;
            bool hasScrollActivity = mouse.scroll.ReadValue() != Vector2.zero;
            return hasMouseMovement || hasMouseButtonActivity || hasScrollActivity;
        }

        private void ResetTimer()
        {
            _lastActivityTime = Time.unscaledTime;
        }

        private void SetDimmed(float rampProgress)
        {
            var targetAlpha = rampProgress * DIM_ALPHA;
            var color = _dimmer.color;
            color.a = targetAlpha;
            _dimmer.color = color;
        }
    }
}
