using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Base for components that adjust a RectTransform by the device
    ///     safe-area insets (canvas units). Re-applies whenever the safe
    ///     area or canvas scale changes — the app boots in portrait before
    ///     settling into landscape, so one-shot adjustments bake in the
    ///     wrong orientation's insets.
    /// </summary>
    public abstract class SafeAreaBehaviour : MonoBehaviour
    {
        private Canvas _canvas;
        private Rect _appliedSafeArea = new(float.NaN, float.NaN, 0, 0);
        private float _appliedScale = float.NaN;

        protected virtual void Awake()
        {
            if (!Application.isMobilePlatform)
            {
                enabled = false;
            }
        }

        /// <summary>
        ///     Runs every frame before the change check, for subclasses whose
        ///     adjustment can be overwritten by other code between safe-area
        ///     changes (e.g. a row that re-lays out its content on refresh).
        /// </summary>
        protected virtual void Reassert()
        {
        }

        private void LateUpdate()
        {
            Reassert();

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null)
                {
                    return;
                }
            }

            float scale = _canvas.rootCanvas.scaleFactor;
            var safeArea = Screen.safeArea;
            if (safeArea == _appliedSafeArea && Mathf.Approximately(scale, _appliedScale))
            {
                return;
            }

            // (left, bottom, right, top)
            Apply(SafeAreaHelper.GetInsets(this));
            _appliedSafeArea = safeArea;
            _appliedScale = scale;
        }

        protected abstract void Apply(Vector4 insets);
    }
}
