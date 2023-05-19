using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.UI {
	[DefaultExecutionOrder(-25)]
	public class HelpBar : MonoBehaviour {
		public static HelpBar Instance { get; private set; }

		[SerializeField]
		private Transform _buttonContainer;
		[SerializeField]
		private TextMeshProUGUI _infoText;

		[Space]
		[SerializeField]
		private GameObject _buttonPrefab;

		[Space]
		[SerializeField]
		private Color[] _menuActionColors;

		private void Awake() {
			Instance = this;
		}

		private void ResetHelpbar() {
			foreach (Transform button in _buttonContainer) {
				Destroy(button.gameObject);
			}

			_infoText.text = null;
		}

		public void SetInfoFromScheme(NavigationScheme scheme) {
			ResetHelpbar();

			// Spawn all buttons
			foreach (var entry in scheme.Entries) {
				// Don't make buttons for up and down
				if (entry.Type == MenuAction.Up || entry.Type == MenuAction.Down) {
					continue;
				}

				var go = Instantiate(_buttonPrefab, _buttonContainer);
				go.GetComponent<HelpBarButton>().SetInfoFromSchemeEntry(entry, _menuActionColors[(int) entry.Type]);
			}
		}

		public void SetInfoText(string str) {
			_infoText.text = str;
		}
	}
}