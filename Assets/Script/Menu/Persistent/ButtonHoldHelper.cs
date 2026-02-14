using UnityEngine;

namespace YARG.Menu.Persistent
{
    public class ButtonHoldHelper
    {
        public bool IsPressed { get; private set; }

        public bool IsHolding => IsPressed;

        private float Elapsed => Time.unscaledTime - _holdStartTime;

        public float HoldProgress => IsPressed ? Mathf.Clamp01(Elapsed / _holdTime) : 0f;

        public enum HoldResult
        {
            CLICK,
            HOLD
        }

        private readonly float _holdTime;
        private float _holdStartTime;

        public ButtonHoldHelper(float holdTime)
        {
            _holdTime = holdTime;
        }

        public void StartHolding()
        {
            IsPressed = true;
            _holdStartTime = Time.unscaledTime;
        }

        public HoldResult StopHolding()
        {
            IsPressed = false;
            return Elapsed >= _holdTime ? HoldResult.HOLD : HoldResult.CLICK;
        }
    }
}
