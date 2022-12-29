using System.Collections.Generic;
using UnityEngine.UIElements;

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

		public static void SetVisible(this UIDocument document, bool visible) {
			document.rootVisualElement.SetVisible(visible);
		}

		public static void SetVisible(this VisualElement elem, bool visible) {
			elem.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		public static void SetOpacity(this VisualElement elem, float alpha) {
			elem.style.opacity = alpha;
		}
	}
}