using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Settings.SettingVisuals;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
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

			foreach (var tab in SettingsManager.SETTINGS_TABS) {
				foreach (var settingName in tab.settings) {
					var setting = SettingsManager.GetSettingTypeByName(settingName);

					try {
						// Spawn the setting
						var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName).WaitForCompletion();
						var go = Instantiate(settingPrefab, settingsContainer);
						go.GetComponent<ISettingVisual>().SetSetting(settingName);
					} catch { }
				}
			}
		}

		public void ForceUpdateLayout() {
			LayoutRebuilder.ForceRebuildLayoutImmediate(settingsContainer as RectTransform);
		}
	}
}