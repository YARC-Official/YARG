using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.UI {
	public class GenericOption : MonoBehaviour {
		[SerializeField]
		private Image topBorder;
		[SerializeField]
		private Image bottomBorder;
		[SerializeField]
		private GameObject selectedBackground;
		[SerializeField]
		private TextMeshProUGUI text;

		public void SetSelected(bool selected) {
			selectedBackground.SetActive(selected);

			if (selected) {
				topBorder.color = Color.white;
				bottomBorder.color = Color.white;
			} else {
				topBorder.color = new Color32(22, 39, 90, 255);
				bottomBorder.color = new Color32(22, 39, 90, 255);
			}
		}

		public void SetText(string t) {
			text.text = t;
		}
	}
}