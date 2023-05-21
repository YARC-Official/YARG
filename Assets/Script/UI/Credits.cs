using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.UI {
	public class Credits : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI creditsText;
		[SerializeField]
		private TextAsset creditsFile;

		private void OnEnable() {
			// Set navigation scheme
			Navigator.Instance.PushScheme(new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => {
					MainMenu.Instance.ShowMainMenu();
				})
			}, true));
		}

		private void OnDisable() {
			Navigator.Instance.PopScheme();
		}

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