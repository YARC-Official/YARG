using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Util {
	/// <summary>
	/// Thanks Unity...
	/// </summary>
	[RequireComponent(typeof(ScrollRect))]
	public class ScrollRectDragInterceptor : MonoBehaviour, IEndDragHandler, IBeginDragHandler {
		private ScrollRect scroll;

		private void Awake() {
			scroll = GetComponent<ScrollRect>();
		}

		public void OnBeginDrag(PointerEventData data) {
			scroll.StopMovement();
			scroll.enabled = false;
		}

		public void OnEndDrag(PointerEventData data) {
			scroll.enabled = true;
		}
	}
}