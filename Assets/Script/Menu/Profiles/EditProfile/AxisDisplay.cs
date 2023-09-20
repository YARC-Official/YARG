using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Profiles
{
    using Edge = RectTransform.Edge;

    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class AxisDisplay : UIBehaviour
    {
        public enum Mode
        {
            AxisCalibration,
            ButtonCalibration,

            AxisDisplay,
            ButtonDisplay,
        }

        [SerializeField]
        private RectTransform _fillRectform;
        [SerializeField]
        private RectTransform _deadzoneRectform;
        [SerializeField]
        private RectTransform _minimumRectform;
        [SerializeField]
        private RectTransform _maximumRectform;

        [Space]
        [SerializeField]
        private Mode _displayMode;

        private RectTransform _selfRectform;
        private DrivenRectTransformTracker _rectTracker;

        public Mode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                UpdateDisplayMode(value);
            }
        }

        private float _value;
        public float Value
        {
            get => _value;
            set => _value = Math.Clamp(value, -1, 1);
        }

        private float _maximum;
        public float Maximum
        {
            get => _maximum;
            set => _maximum = Math.Clamp(value, -1, 1);
        }

        private float _minimum;
        public float Minimum
        {
            get => _minimum;
            set => _minimum = Math.Clamp(value, -1, 1);
        }

        private float _upperDeadzone;
        public float UpperDeadzone
        {
            get => _upperDeadzone;
            set => _upperDeadzone = Math.Clamp(value, -1, 1);
        }

        private float _lowerDeadzone;
        public float LowerDeadzone
        {
            get => _lowerDeadzone;
            set => _lowerDeadzone = Math.Clamp(value, -1, 1);
        }

        public float PressPoint
        {
            get => Minimum;
            set => Minimum = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            UpdateCachedReferences();
            UpdateDisplayMode(DisplayMode);
#if UNITY_EDITOR
            SetDefaultDisplayValues();
#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _rectTracker.Clear();
        }

        private void Update()
        {
            switch (DisplayMode)
            {
                case Mode.AxisCalibration:
                    SetFill(_fillRectform, 0, Value, true);
                    SetFill(_deadzoneRectform, LowerDeadzone, UpperDeadzone);
                    SetFill(_minimumRectform, -1, Minimum);
                    SetFill(_maximumRectform, 1, Maximum);
                    break;

                case Mode.ButtonCalibration:
                    SetFill(_fillRectform, -1, Value);
                    SetFill(_minimumRectform, -1, Minimum);
                    break;

                case Mode.AxisDisplay:
                    SetFill(_fillRectform, 0, Value, true);
                    break;

                case Mode.ButtonDisplay:
                    SetFill(_fillRectform, -1, Value);
                    break;
            }
        }

        private void SetFill(RectTransform rectForm, float start, float end, bool snapStartToCenter = false)
        {
            // Swap if needed
            if (start > end)
                (start, end) = (end, start);

            _rectTracker.Add(this, rectForm, DrivenTransformProperties.All);

            // Get size of the fill
            var selfRect = _selfRectform.rect;
            // A note: Absolute position values should not be used,
            // otherwise things will not lay out correctly
            float startPos = Mathf.Lerp(0, selfRect.width, (1 + start) / 2);
            float endPos = Mathf.Lerp(0, selfRect.width, (1 + end) / 2);

            // Limit width of the fill relative to the center
            if (snapStartToCenter)
            {
                float maxStart = (selfRect.width / 2) - (selfRect.height / 2);
                float minEnd = (selfRect.width / 2) + (selfRect.height / 2);

                if (startPos > maxStart)
                    startPos = maxStart;

                if (endPos < minEnd)
                    endPos = minEnd;
            }

            // Ensure scale is set correctly, it got nuked to 0 at some point and
            // took me an hour of wasted effort trying fixes to notice lol
            rectForm.localScale = new(1, 1, 1);

            // Set dimensions of the fill
            rectForm.SetInsetAndSizeFromParentEdge(Edge.Top, 0, selfRect.height);
            rectForm.SetInsetAndSizeFromParentEdge(Edge.Left, startPos, endPos - startPos);
        }

        private void UpdateDisplayMode(Mode mode)
        {
            _deadzoneRectform.gameObject.SetActive(mode is Mode.AxisCalibration);
            _minimumRectform.gameObject.SetActive(mode is Mode.AxisCalibration or Mode.ButtonCalibration);
            _maximumRectform.gameObject.SetActive(mode is Mode.AxisCalibration);
        }

        private void UpdateCachedReferences()
        {
            _selfRectform = (RectTransform) transform;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // OnValidate is called before OnEnabled,
            // we need to make sure to not touch any other objects before then
            if (IsActive())
            {
                UpdateCachedReferences();
                UpdateDisplayMode(DisplayMode);
                SetDefaultDisplayValues();

                // Everything else will be updated in Update, certain things can't be called in OnValidate
            }
        }

        private void SetDefaultDisplayValues()
        {
            // Only set default values in edit mode
            if (Application.isPlaying)
                return;

            Value = 0;
            if (DisplayMode == Mode.AxisCalibration)
            {
                Maximum = 0.9f;
                Minimum = -0.9f;
                UpperDeadzone = 0.1f;
                LowerDeadzone = -0.1f;
            }
            else if (DisplayMode == Mode.ButtonCalibration)
            {
                Minimum = 0.5f;
            }
        }
#endif
    }
}