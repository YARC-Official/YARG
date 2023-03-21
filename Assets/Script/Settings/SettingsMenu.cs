using UnityEngine;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private Transform settingsContainer;

		private void OnEnable() {
			UpdateSettings();
		}

		private void UpdateSettings() {
			foreach (Transform t in settingsContainer) {
				Destroy(t.gameObject);
			}
		}
	}
}