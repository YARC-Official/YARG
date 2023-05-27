using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class DevWatermark : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI watermarkText;

		void Start() {
			// check if Constants.VERSION_TAG ends with "b"
			if (Constants.VERSION_TAG.beta) {
				watermarkText.text = $"<b>YARG {Constants.VERSION_TAG}</b>  Developer Build";
				watermarkText.gameObject.SetActive(true);
			} else {
				this.gameObject.SetActive(false);
			}

			// disable script
			this.enabled = false;
		}
	}
}
