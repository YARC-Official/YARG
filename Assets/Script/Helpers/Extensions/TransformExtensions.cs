using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Converts a screen-space point to a local-space point within this rectangle transform.
        /// </summary>
        /// <param name="rect">The target <see cref="RectTransform"/> to convert into.</param>
        /// <param name="screenPoint">The screen-space point to convert.</param>
        /// <param name="cam">The camera associated with the screen point, or <see langword="null"/> for overlay canvases.</param>
        /// <returns>
        /// The converted local-space point if successful; otherwise, <see langword="null"/>.
        /// </returns>
        public static Vector2? ScreenPointToLocalPoint(this RectTransform rect, Vector2 screenPoint, Camera cam = null)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var localPoint)
                ? localPoint
                : null;
        }

        /// <summary>
        /// Returns the screen-space position of this RectTransform's center.
        /// </summary>
        public static Vector2 GetScreenCenter(this RectTransform rect, Camera cam = null)
        {
            var centerWorldPoint = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(cam, centerWorldPoint);
        }

        /// <summary>
        /// Checks whether a screen-space point is contained within this RectTransform.
        /// </summary>
        public static bool ContainsScreenPoint(this RectTransform rect, Vector2 screenPoint, Camera cam = null)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
        }

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
