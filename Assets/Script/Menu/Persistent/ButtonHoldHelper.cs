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
            HOLD,
            CANCELLED
        }

        private readonly float _holdTime;
        private readonly float _cancelThreshold;
        private float _holdStartTime;

        public ButtonHoldHelper(float holdTime, float cancelThreshold = 0f)
        {
            _holdTime = holdTime;
            _cancelThreshold = cancelThreshold;
        }

        public void StartHolding()
        {
            IsPressed = true;
            _holdStartTime = Time.unscaledTime;
        }

        public HoldResult StopHolding()
        {
            IsPressed = false;
            if (Elapsed >= _holdTime)
            {
                return HoldResult.HOLD;
            }
            return Elapsed >= _cancelThreshold ? HoldResult.CANCELLED : HoldResult.CLICK;
        }
    }
}
