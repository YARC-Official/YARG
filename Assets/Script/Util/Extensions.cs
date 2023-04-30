using System.Collections.Generic;
using UnityEngine;

namespace YARG.Util {
	public static class Extensions {
		public static T PeekOrNull<T>(this Queue<T> queue) where T : class {
			if (queue.TryPeek(out var o)) {
				return o;
			}

			return null;
		}

		public static T ReversePeekOrNull<T>(this Queue<T> queue) where T : class {
			if (queue.Count <= 0) {
				return null;
			}

			return queue.ToArray()[^1];
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to screen space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the screen space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect ToScreenSpace(this RectTransform transform) {
			// https://answers.unity.com/questions/1013011/convert-recttransform-rect-to-screen-space.html
			Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale.Abs());
			return new Rect((Vector2) transform.position - (size * transform.pivot), size);
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the viewport space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect ToViewportSpace(this RectTransform transform) {
			Rect rect = ToScreenSpace(transform);
			rect.width /= Screen.width;
			rect.height /= Screen.height;
			rect.x /= Screen.width;
			rect.y /= Screen.height;

			return rect;
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
		/// <param name="h">Center horizontally.</param>
		/// <param name="v">Center vertically.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the viewport space of the specified <see cref="RectTransform"/>, centered on it.
		/// </returns>
		public static Rect ToViewportSpaceCentered(this RectTransform transform, bool h = true, bool v = true) {
			Rect rect = transform.ToViewportSpace();

			if (h) {
				rect.x = 0.5f - rect.width / 2f;
			}

			if (v) {
				rect.y = 0.5f - rect.height / 2f;
			}

			return rect;
		}
	}
}