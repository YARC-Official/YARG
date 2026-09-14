using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Pulls the selected edges of a stretched RectTransform inward by
    ///     the safe-area insets, on top of its authored offsets. The bulk
    ///     approach for keeping menu content out of the notch and home
    ///     indicator while full-bleed backgrounds stay untouched.
    /// </summary>
    public class SafeAreaContainer : SafeAreaBehaviour
    {
        [SerializeField]
        private bool _fromLeft;
        [SerializeField]
        private bool _fromRight;
        [SerializeField]
        private bool _fromTop;
        [SerializeField]
        private bool _fromBottom;

        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;
        private bool _baseCaptured;
        private Vector2 _lastMin;
        private Vector2 _lastMax;
        private Vector2 _shiftMin;
        private Vector2 _shiftMax;

        public void Configure(bool left = false, bool right = false, bool top = false, bool bottom = false)
        {
            _fromLeft = left;
            _fromRight = right;
            _fromTop = top;
            _fromBottom = bottom;
        }

        /// <summary>
        ///     Attaches a container to <paramref name="rect"/>, keeping only
        ///     the edges whose axis the rect actually stretches across —
        ///     offset changes on a fixed-size axis would resize the element
        ///     instead of insetting it. No-op on non-mobile platforms or
        ///     when no requested edge survives the stretch check.
        /// </summary>
        public static void TryAttach(RectTransform rect, bool left = false, bool right = false,
            bool top = false, bool bottom = false)
        {
            if (!Application.isMobilePlatform || rect == null)
            {
                return;
            }

            bool stretchesX = !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
            bool stretchesY = !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
            left &= stretchesX;
            right &= stretchesX;
            top &= stretchesY;
            bottom &= stretchesY;

            if (!left && !right && !top && !bottom)
            {
                return;
            }

            rect.gameObject.AddComponent<SafeAreaContainer>()
                .Configure(left, right, top, bottom);
        }

        protected override void Apply(Vector4 insets)
        {
            var rect = (RectTransform) transform;
            if (!_baseCaptured)
            {
                _baseOffsetMin = rect.offsetMin;
                _baseOffsetMax = rect.offsetMax;
                _baseCaptured = true;
            }

            _shiftMin = new Vector2(_fromLeft ? insets.x : 0f, _fromBottom ? insets.y : 0f);
            _shiftMax = new Vector2(_fromRight ? -insets.z : 0f, _fromTop ? -insets.w : 0f);

            rect.offsetMin = _baseOffsetMin + _shiftMin;
            rect.offsetMax = _baseOffsetMax + _shiftMax;
            _lastMin = rect.offsetMin;
            _lastMax = rect.offsetMax;
        }

        // Rows re-lay out their content on refresh and overwrite the
        // offsets; treat each changed component as its new authored base
        // and re-add the inset once. Components the rewrite left alone keep
        // their base, so a top-padding change cannot double a side inset.
        protected override void Reassert()
        {
            if (!_baseCaptured)
            {
                return;
            }

            var rect = (RectTransform) transform;
            if (rect.offsetMin == _lastMin && rect.offsetMax == _lastMax)
            {
                return;
            }

            _baseOffsetMin = Rebase(rect.offsetMin, _lastMin, _baseOffsetMin);
            _baseOffsetMax = Rebase(rect.offsetMax, _lastMax, _baseOffsetMax);
            rect.offsetMin = _baseOffsetMin + _shiftMin;
            rect.offsetMax = _baseOffsetMax + _shiftMax;
            _lastMin = rect.offsetMin;
            _lastMax = rect.offsetMax;
        }

        private static Vector2 Rebase(Vector2 current, Vector2 last, Vector2 baseValue)
        {
            return new Vector2(
                Mathf.Approximately(current.x, last.x) ? baseValue.x : current.x,
                Mathf.Approximately(current.y, last.y) ? baseValue.y : current.y);
        }
    }
}
