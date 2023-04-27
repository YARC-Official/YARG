using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Settings.SettingVisuals;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private Transform tabsContainer;
		[SerializeField]
		private Transform settingsContainer;

		[Space]
		[SerializeField]
		private GameObject tabPrefab;
		[SerializeField]
		private GameObject buttonPrefab;

		private string _currentTab;
		public string CurrentTab {
			get => _currentTab;
			set {
				_currentTab = value;

				UpdateSettings();
			}
		}

		private void OnEnable() {
			_currentTab = SettingsManager.SETTINGS_TABS[0].name;

			UpdateTabs();
			UpdateSettings();
		}

		private void UpdateTabs() {
			// Destroy all previous tabs
			foreach (Transform t in tabsContainer) {
				Destroy(t.gameObject);
			}

			// Then, create new tabs!
			foreach (var tab in SettingsManager.SETTINGS_TABS) {
				var go = Instantiate(tabPrefab, tabsContainer);
				go.GetComponent<SettingsTab>().SetTab(tab.name);
			}
		}

		private void UpdateSettings() {
			// Destroy all previous settings
			foreach (Transform t in settingsContainer) {
				Destroy(t.gameObject);
			}

			foreach (var tab in SettingsManager.SETTINGS_TABS) {
				// Look for the tab
				if (tab.name != CurrentTab) {
					continue;
				}

				// Once we've found the tab, add the settings
				foreach (var settingName in tab.settings) {
					if (settingName.StartsWith("$")) {
						// Spawn the button
						var go = Instantiate(buttonPrefab, settingsContainer);
						go.GetComponent<SettingsButton>().SetInfo(settingName);
					} else {
						var setting = SettingsManager.GetSettingByName(settingName);

						// Spawn the setting
						var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName).WaitForCompletion();
						var go = Instantiate(settingPrefab, settingsContainer);
						go.GetComponent<ISettingVisual>().SetSetting(settingName);
					}
				}

				// Then we're good!
				break;
			}
		}
	}
}