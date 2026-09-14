using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Grows a bottom-anchored bar upward by the bottom safe-area inset
    ///     so its background stays flush with the physical screen edge while
    ///     the content inside is padded above the home indicator. Shifting
    ///     the whole bar up instead would leave a see-through gap beneath it.
    /// </summary>
    public class SafeAreaBarHeight : SafeAreaBehaviour
    {
        private Vector2 _baseSizeDelta;
        private bool _baseCaptured;

        protected override void Apply(Vector4 insets)
        {
            var rect = (RectTransform) transform;
            if (!_baseCaptured)
            {
                _baseSizeDelta = rect.sizeDelta;
                _baseCaptured = true;
            }

            rect.sizeDelta = new Vector2(_baseSizeDelta.x, _baseSizeDelta.y + insets.y);
        }
    }
}
