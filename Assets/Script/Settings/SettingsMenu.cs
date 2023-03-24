using UnityEngine;
using UnityEngine.AddressableAssets;

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

			foreach (var (name, info) in SettingsManager.AllSettings) {
				var settingPrefab = Addressables.LoadAssetAsync<GameObject>($"Setting/{info.type}").WaitForCompletion();
				var go = Instantiate(settingPrefab, settingsContainer);
				go.GetComponent<AbstractSetting>().Setup(name, name);
			}
		}
	}
}