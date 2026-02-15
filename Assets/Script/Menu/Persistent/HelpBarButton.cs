using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    public class HelpBarButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        // Delay before showing hold fill so short taps do not flash the bar.
        private const float DELAY_FILL_SECONDS = 0.25f;

        [SerializeField]
        private Image _buttonImage;
        [SerializeField]
        private Image _buttonBackground;
        [SerializeField]
        private Image _buttonHoldFill;
        [SerializeField]
        private Image _buttonOutline;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TextMeshProUGUI _buttonLabel;
        [SerializeField]
        private TextMeshProUGUI _buttonText;

        private HoldTracker _holdTracker;

        private NavigationScheme.Entry? _entry;

        private Color _buttonBackgroundColor;
        private Color _buttonImageColor;
        private Color _buttonBackgroundColorOnDown;
        private Color _buttonFillColor;

        // Visually transparent, but not affected by the mask component
        private readonly Color _maskableClear = new(0f, 0f, 0f, 0.01f);
        private readonly Color _coolGrey = new(123 / 255f, 127 / 255f, 154 / 255f, 1f);

        private bool _clickable = true;
        private bool _isPointerOver;
        private bool _isPointerHolding;
        private ButtonState _defaultState = ButtonState.NONE;

        private ButtonState _currentState = ButtonState.NONE;

        public enum ButtonState
        {
            NONE,
            HOVER,
            PRESS,
            HOLD,
            DISABLED,
        }

        public void SetInfoFromSchemeEntry(NavigationScheme.Entry entry, bool clickable = true)
        {
            _clickable = clickable;
            _entry = entry;

            if (entry.HasHoldHandler)
            {
                _holdTracker ??= new HoldTracker(0f);
                _holdTracker.ClearEvents();
                _holdTracker.Configure(entry.HoldSeconds, DELAY_FILL_SECONDS);
                _holdTracker.OnHoldProgress += HandleHoldProgress;
                _holdTracker.OnClick += HandleClick;
                _holdTracker.OnHoldComplete += HandleHoldComplete;
                _holdTracker.OnHoldCancelled += HandleHoldCancelled;
            }

            _isPointerHolding = false;
            _defaultState = ButtonState.NONE;

            var icons = MenuData.NavigationIcons;
            _buttonBackgroundColor = icons.GetColor(entry.Action);
            _buttonBackgroundColor.a = 0.05f;
            _buttonBackgroundColorOnDown = icons.GetColor(entry.Action);
            _buttonBackgroundColorOnDown.a = 0.2f;
            _buttonFillColor = icons.GetColor(entry.Action);
            _buttonFillColor.a = 0.3f;
            _buttonImageColor = icons.GetColor(entry.Action);
            _buttonImageColor.a = 1f;

            // Label
            _buttonLabel.text = entry.DisplayName;

            // Show/hide text and transitions
            var special = entry.Action is MenuAction.Select or MenuAction.Start or MenuAction.Left or MenuAction.Right;
            _buttonText.gameObject.SetActive(!special);
            _button.transition = special
                ? Selectable.Transition.None
                : Selectable.Transition.SpriteSwap;

            // Set sprite and fill color, then apply idle state
            _buttonImage.sprite = icons.GetIcon(entry.Action);
            _buttonHoldFill.color = _buttonFillColor;
            ApplyState(_defaultState);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
            if (!_clickable)
            {
                return;
            }

            ApplyState(ButtonState.HOVER);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
            if (!_clickable)
            {
                return;
            }

            ApplyState(_defaultState);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            if (_entry?.HasHoldHandler == true)
            {
                _isPointerHolding = true;
                _holdTracker.StartHolding();
                ApplyState(ButtonState.HOLD);
            }
            else
            {
                ApplyState(ButtonState.PRESS);
                _entry?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_clickable)
            {
                return;
            }

            if (_isPointerHolding)
            {
                _holdTracker.StopHolding();
                _isPointerHolding = false;
            }

            _buttonHoldFill.fillAmount = 0f;
            ApplyState(_isPointerOver ? ButtonState.HOVER : _defaultState);
            _entry?.InvokeHoldOffHandler();
        }

        public void DisableButton()
        {
            _clickable = false;
            ApplyState(ButtonState.DISABLED);
        }

        public void EnableButton()
        {
            _clickable = true;
            ApplyState(_defaultState);
        }

        public void SetDefaultButtonState(ButtonState state)
        {
            _defaultState = state;
            ApplyState(_defaultState);
        }

        private void HandleHoldProgress(float visualProgress)
        {
            UpdateButtonFillAmount(visualProgress);
        }

        private void HandleClick()
        {
            _entry?.Invoke();
        }

        private void HandleHoldComplete()
        {
            _isPointerHolding = false;
            _entry?.InvokeHoldHandler();
            _buttonHoldFill.fillAmount = 0f;
            ApplyState(_isPointerOver ? ButtonState.HOVER : _defaultState);
        }

        private void HandleHoldCancelled()
        {
            // Visual reset happens in OnPointerUp
        }

        private void ApplyState(ButtonState state)
        {
            _currentState = state;

            switch (state)
            {
                case ButtonState.NONE:
                    _buttonBackground.color = _maskableClear;
                    _buttonOutline.color = _maskableClear;
                    _buttonImage.color = _buttonImageColor;
                    _buttonLabel.color = _coolGrey;
                    _buttonText.color = _coolGrey;
                    break;
                case ButtonState.HOVER:
                    _buttonBackground.color = _buttonBackgroundColor;
                    _buttonOutline.color = _buttonBackgroundColor;
                    _buttonImage.color = _buttonImageColor;
                    _buttonLabel.color = Color.white;
                    _buttonText.color = Color.white;
                    break;
                case ButtonState.PRESS:
                    _buttonBackground.color = _buttonBackgroundColorOnDown;
                    _buttonOutline.color = _buttonBackgroundColorOnDown;
                    _buttonImage.color = _buttonImageColor;
                    _buttonLabel.color = Color.white;
                    _buttonText.color = Color.white;
                    break;
                case ButtonState.HOLD:
                    _buttonBackground.color = _buttonBackgroundColor;
                    _buttonOutline.color = _buttonBackgroundColor;
                    _buttonImage.color = _buttonImageColor;
                    _buttonLabel.color = Color.white;
                    _buttonText.color = Color.white;
                    break;
                case ButtonState.DISABLED:
                    _buttonBackground.color = Color.gray;
                    _buttonOutline.color = Color.gray;
                    _buttonImage.color = Color.gray;
                    _buttonLabel.color = _coolGrey;
                    _buttonText.color = _coolGrey;
                    break;
            }
        }

        private void Update()
        {
            if (_entry?.HasHoldHandler != true)
            {
                return;
            }

            _holdTracker?.Tick();

            if (!_isPointerHolding)
            {
                HandleControllerHold();
            }
        }

        // Controller hold, not pointer hold.  This only handles visuals, the actual hold action is
        // triggered by the Navigator when the hold is complete
        private void HandleControllerHold()
        {
            var rawHoldProgress = _entry.HasValue
                ? Navigator.Instance.GetHoldProgress(_entry.Value.Action)
                : -1f;
            if (rawHoldProgress >= 0f)
            {
                if (_currentState != ButtonState.HOLD)
                {
                    ApplyState(ButtonState.HOLD);
                }
                var visualProgress = _holdTracker.GetVisualHoldProgress(rawHoldProgress);
                UpdateButtonFillAmount(visualProgress);
            }
            else if (_buttonHoldFill.fillAmount > 0f || _currentState == ButtonState.HOLD)
            {
                _buttonHoldFill.fillAmount = 0f;
                ApplyState(_isPointerOver ? ButtonState.HOVER : _defaultState);
            }
        }

        private void UpdateButtonFillAmount(float amount)
        {
            _buttonHoldFill.fillAmount = amount;
            if (amount > 0f && _currentState != ButtonState.HOLD)
            {
                ApplyState(ButtonState.HOLD);
            }
        }
    }
}
