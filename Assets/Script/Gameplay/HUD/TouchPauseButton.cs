using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    /// <summary>
    ///     On-screen pause button for touch platforms, where no Escape key or
    ///     Start button exists. Mirrors the keyboard pause path and stays
    ///     hidden everywhere else.
    /// </summary>
    public class TouchPauseButton : GameplayBehaviour
    {
        [SerializeField]
        private Image _image;
        [SerializeField]
        private Button _button;

        protected override void GameplayAwake()
        {
            if (!Application.isMobilePlatform)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // In Start rather than Awake so the CanvasScaler has applied the
            // real scale factor
            ApplySafeAreaInset();
        }

        // The button anchors to the canvas' top-right corner; in landscape the
        // notch/Dynamic Island can occupy that edge, so nudge it into the safe
        // area
        private void ApplySafeAreaInset()
        {
            var insets = Helpers.SafeAreaHelper.GetInsets(this);
            var rect = (RectTransform) transform;
            rect.anchoredPosition -= new Vector2(insets.z, insets.w);
        }

        private void Update()
        {
            // The pause menu has its own UI; hide the button while it's up.
            // A disabled graphic also stops receiving raycasts.
            bool showButton = !GameManager.Paused;
            if (_image.enabled != showButton)
            {
                _image.enabled = showButton;
                _button.interactable = showButton;
            }
        }

        // Wired to the Button's OnClick in the prefab
        public void OnPauseClicked()
        {
            GameManager.TogglePause();
        }
    }
}
