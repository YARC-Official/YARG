using UnityEngine;
using UnityEngine.AddressableAssets;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private GameObject settingSpacePrefab;

		[SerializeField]
		private Transform settingsContainer;

		private void OnEnable() {
			UpdateSettings();
		}

		private void UpdateSettings() {
			// Destroy all previous settings
			foreach (Transform t in settingsContainer) {
				Destroy(t.gameObject);
			}

			foreach (var info in SettingsManager.GetAllSettings()) {
				// Spawn a space if needed
				if (info.spaceAbove) {
					Instantiate(settingSpacePrefab, settingsContainer);
				}

				// Spawn the setting
				var settingPrefab = Addressables.LoadAssetAsync<GameObject>($"Setting/{info.type}").WaitForCompletion();
				var go = Instantiate(settingPrefab, settingsContainer);
				go.GetComponent<AbstractSetting>().Setup(info.name);
			}
		}
	}
}