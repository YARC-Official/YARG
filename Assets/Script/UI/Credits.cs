using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class Credits : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI creditsText;
		[SerializeField]
		private TextAsset creditsFile;

		private void Start() {
			var split = creditsFile.text.Split("<<COLUMN>>");

			// Trim the strings
			for (int i = 0; i < split.Length; i++) {
				split[i] = split[i].Trim();
			}

			// Create first column
			creditsText.text = split[0];

			// Create the rest of the columns
			for (int i = 1; i < split.Length; i++) {
				var column = Instantiate(creditsText, creditsText.transform.parent);
				column.text = split[i];
			}
		}
	}
}