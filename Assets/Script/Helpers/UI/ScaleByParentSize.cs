using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    /// Resizes a RectTransform to fit a specified aspect ratio.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ScaleByParentSize : MonoBehaviour
    {
        private RectTransform _parentRectTransform;
        private RectTransform ParentRectTransform
        {
            get
            {
                if (_parentRectTransform == null)
                {
                    _parentRectTransform = transform.parent.gameObject.GetComponent<RectTransform>();
                }

                return _parentRectTransform;
            }
        }

        [SerializeField]
        private Vector2 _initialSize = Vector2.one;
        [SerializeField]
        private ScaleMode _scaleMode = ScaleMode.ScaleByHeight;
        public void Initialize()
        {
            _initialSize = ParentRectTransform.rect.size;
        }

        private void Update()
        {
            UpdateScale();
        }

        private void UpdateScale()
        {
            var size = ParentRectTransform.rect.size;
            float scale;
            if (_scaleMode == ScaleMode.ScaleByWidth)
            {
                scale = size.x / _initialSize.x;
            }
            else
            {
                scale = size.y / _initialSize.y;
            }
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public enum ScaleMode
        {
            ScaleByHeight,
            ScaleByWidth
        }
    }
}
