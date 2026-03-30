using DG.Tweening;
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
        private const float IDLE_DELAY_SECONDS = 120f;
        private const float DIM_ALPHA = 0.9f; //90% opaque, 10% transparent
        private const float FADE_DURATION_SECONDS = 0.5f;

        [SerializeField]
        private Image _dimmer;

        private float _lastActivityTime;
        private Tween _fadeTween;

        protected override void SingletonAwake()
        {
            ResetTimer();
            SetDimmed(false);
        }

        private void OnEnable()
        {
            InputManager.MenuInput += OnMenuInput;
        }

        private void OnDisable()
        {
            InputManager.MenuInput -= OnMenuInput;
            _fadeTween?.Kill();
            _fadeTween = null;
        }

        private void Update()
        {
            var currentScene = GlobalVariables.Instance.CurrentScene;
            bool isGameplay = currentScene is SceneIndex.Gameplay;
            bool isNotFocused = Application.isFocused;
            bool didReceiveInput = CheckKeyboardMouse();

            if (didReceiveInput || isGameplay || isNotFocused)
            {
                ResetTimer();
            }

            var idleDuration = Time.unscaledTime - _lastActivityTime;
            var shouldDim = idleDuration >= IDLE_DELAY_SECONDS;
            SetDimmed(shouldDim);
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

        private void SetDimmed(bool dimmed)
        {
            _fadeTween?.Kill();
            _fadeTween = _dimmer
                .DOFade(dimmed ? DIM_ALPHA : 0f, dimmed ? FADE_DURATION_SECONDS : 0f)
                .SetEase(Ease.Linear)
                .SetUpdate(true);
        }
    }
}
