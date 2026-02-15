using System;
using UnityEngine;

namespace YARG.Menu.Persistent
{
    /// <summary>
    /// State machine that tracks elapsed time, hold progress, and tap vs cancel result
    /// The owner must call <see cref="Tick"/> each frame.
    /// </summary>
    public class HoldTracker
    {
        public const float DEFAULT_CANCEL_THRESHOLD = 0.25f;

        public event Action<float> OnHoldProgress;
        public event Action OnClick;
        public event Action OnHoldComplete;
        public event Action OnHoldCancelled;

        public bool IsPressed { get; private set; }
        public bool IsHolding => IsPressed;

        public float HoldProgress => IsPressed ? Mathf.Clamp01(Elapsed / _holdTime) : 0f;

        private float Elapsed => Time.unscaledTime - _holdStartTime;

        private float _holdTime;
        private float _cancelThreshold;
        private float _holdStartTime;
        private bool _holdCompleted;

        public HoldTracker(float holdTime, float cancelThreshold = DEFAULT_CANCEL_THRESHOLD)
        {
            _holdTime = holdTime;
            _cancelThreshold = cancelThreshold;
        }

        public void Configure(float holdTime, float cancelThreshold = DEFAULT_CANCEL_THRESHOLD)
        {
            _holdTime = holdTime;
            _cancelThreshold = cancelThreshold;
        }

        public void ClearEvents()
        {
            OnHoldProgress = null;
            OnClick = null;
            OnHoldComplete = null;
            OnHoldCancelled = null;
        }

        public void StartHolding()
        {
            IsPressed = true;
            _holdCompleted = false;
            _holdStartTime = Time.unscaledTime;
        }

        public void StopHolding()
        {
            if (!IsPressed)
            {
                return;
            }

            IsPressed = false;

            if (_holdCompleted)
            {
                return;
            }

            if (Elapsed >= _cancelThreshold)
            {
                OnHoldCancelled?.Invoke();
            }
            else
            {
                OnClick?.Invoke();
            }
        }

        /// <summary>
        /// Must be called each frame by the owner
        /// </summary>
        public void Tick()
        {
            if (!IsPressed || _holdCompleted)
            {
                return;
            }

            float progress = HoldProgress;
            OnHoldProgress?.Invoke(GetVisualHoldProgress(progress));

            if (progress >= 1f)
            {
                _holdCompleted = true;
                IsPressed = false;
                OnHoldComplete?.Invoke();
            }
        }

        /// <summary>
        /// Remaps raw hold progress to include a visual delay, so the fill bar
        /// doesn't flash on quick taps.
        /// </summary>
        public float GetVisualHoldProgress(float rawProgress)
        {
            if (_holdTime <= 0f)
            {
                return rawProgress;
            }

            float delayProgress = Mathf.Clamp01(_cancelThreshold / _holdTime);
            if (rawProgress <= delayProgress)
            {
                return 0f;
            }

            float adjustedRange = 1f - delayProgress;
            return Mathf.Clamp01((rawProgress - delayProgress) / (adjustedRange + Mathf.Epsilon));
        }
    }
}
