using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class Credits : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI creditsText;
		[SerializeField]
		private TextAsset creditsFile;

		private void Start() {
			creditsText.text = creditsFile.text;
		}
	}
}