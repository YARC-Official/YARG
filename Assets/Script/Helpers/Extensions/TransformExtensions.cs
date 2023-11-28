using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class TransformExtensions
    {
        /// <param name="transform">The <see cref="RectTransform"/> to convert to screen space.</param>
        /// <returns>
        /// A <see cref="Rect"/> representing the screen space of the specified <see cref="RectTransform"/>.
        /// </returns>
        public static Rect ToScreenSpace(this RectTransform transform)
        {
            // https://answers.unity.com/questions/1013011/convert-recttransform-rect-to-screen-space.html
            var size = Vector2.Scale(transform.rect.size, transform.lossyScale.Abs());
            return new Rect((Vector2) transform.position - size * transform.pivot, size);
        }

        /// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
        /// <returns>
        /// A <see cref="Rect"/> representing the viewport space of the specified <see cref="RectTransform"/>.
        /// </returns>
        public static Rect ToViewportSpace(this RectTransform transform)
        {
            var rect = ToScreenSpace(transform);
            rect.width /= Screen.width;
            rect.height /= Screen.height;
            rect.x /= Screen.width;
            rect.y /= Screen.height;

            return rect;
        }

        /// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
        /// <param name="h">Center horizontally.</param>
        /// <param name="v">Center vertically.</param>
        /// <param name="scale">The scale multiplier for the render texture.</param>
        /// <returns>
        /// A <see cref="Rect"/> representing the viewport space of the specified <see cref="RectTransform"/>, centered on it.
        /// </returns>
        public static Rect ToViewportSpaceCentered(this RectTransform transform, bool h = true, bool v = true, float scale = 1f)
        {
            var rect = transform.ToViewportSpace();
            rect.width /= scale;
            rect.height /= scale;

            if (h)
            {
                rect.x = 0.5f - rect.width / 2f;
            }

            if (v)
            {
                rect.y = 0.5f - rect.height / 2f;
            }

            return rect;
        }

        /// <summary>
        /// Destroys all of the children of the specified transform.
        /// </summary>
        /// <param name="transform">The <see cref="Transform"/> to destroy the children of.</param>
        public static void DestroyChildren(this Transform transform)
        {
            foreach (Transform t in transform)
            {
                Object.Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// Changes the layer of this transform and all of its children.
        /// </summary>
        public static void SetLayerRecursive(this Transform transform, int layer)
        {
            var children = transform.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var child in children)
            {
                child.gameObject.layer = layer;
            }
        }
    }
}