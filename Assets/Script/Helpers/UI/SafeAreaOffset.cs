using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Shifts a UI element inward by the device safe-area inset on the
    ///     selected edges, in canvas units. For fixed-size elements near a
    ///     screen edge (a corner button, a content-size-fitted button row)
    ///     where moving the anchored position is safer than changing offsets.
    /// </summary>
    public class SafeAreaOffset : SafeAreaBehaviour
    {
        [SerializeField]
        private bool _fromLeft;
        [SerializeField]
        private bool _fromRight;
        [SerializeField]
        private bool _fromTop;
        [SerializeField]
        private bool _fromBottom;

        private Vector2 _basePosition;
        private bool _baseCaptured;
        private Vector2 _lastApplied;
        private Vector2 _lastShift;

        /// <summary>For components added from code rather than a prefab.</summary>
        public void Configure(bool left = false, bool right = false, bool top = false, bool bottom = false)
        {
            _fromLeft = left;
            _fromRight = right;
            _fromTop = top;
            _fromBottom = bottom;
        }

        protected override void Apply(Vector4 insets)
        {
            var rect = (RectTransform) transform;
            if (!_baseCaptured)
            {
                _basePosition = rect.anchoredPosition;
                _baseCaptured = true;
            }

            _lastShift = Vector2.zero;
            if (_fromLeft) _lastShift.x += insets.x;
            if (_fromRight) _lastShift.x -= insets.z;
            if (_fromBottom) _lastShift.y += insets.y;
            if (_fromTop) _lastShift.y -= insets.w;

            rect.anchoredPosition = _basePosition + _lastShift;
            _lastApplied = rect.anchoredPosition;
        }

        // Other code may re-lay out this element (list rows reposition
        // their content on every refresh), overwriting the shift. When the
        // position no longer matches what was last applied, treat the new
        // value as the authored base and re-add the shift once.
        protected override void Reassert()
        {
            if (!_baseCaptured)
            {
                return;
            }

            var rect = (RectTransform) transform;
            if (rect.anchoredPosition != _lastApplied)
            {
                _basePosition = rect.anchoredPosition;
                rect.anchoredPosition = _basePosition + _lastShift;
                _lastApplied = rect.anchoredPosition;
            }
        }
    }
}
