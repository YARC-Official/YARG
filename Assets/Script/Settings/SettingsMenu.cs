using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Metadata;
using YARG.Settings.Visuals;

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
		[SerializeField]
		private GameObject headerPrefab;

		private string _currentTab;
		public string CurrentTab {
			get => _currentTab;
			set {
				_currentTab = value;

				UpdateSettings();
			}
		}

		private void OnEnable() {
			// Select the first tab
			foreach (var tab in SettingsManager.SETTINGS_TABS) {
				// Skip tabs that aren't shown in game, if we are in game
				if (!tab.showInGame && GameManager.Instance.CurrentScene == SceneIndex.PLAY) {
					continue;
				}

				_currentTab = tab.name;
				break;
			}

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
				// Skip tabs that aren't shown in game, if we are in game
				if (!tab.showInGame && GameManager.Instance.CurrentScene == SceneIndex.PLAY) {
					continue;
				}

				var go = Instantiate(tabPrefab, tabsContainer);
				go.GetComponent<SettingsTab>().SetTab(tab.name, tab.icon);
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
				foreach (var settingMetadata in tab.settings) {
					if (settingMetadata is ButtonRowMetadata buttonRow) {
						// Spawn the button
						var go = Instantiate(buttonPrefab, settingsContainer);
						go.GetComponent<SettingsButton>().SetInfo(buttonRow.Buttons[0]);
					} else if (settingMetadata is HeaderMetadata header) {
						// Spawn the header
						var go = Instantiate(headerPrefab, settingsContainer);

						// Set header text
						go.GetComponentInChildren<LocalizeStringEvent>().StringReference = new LocalizedString {
							TableReference = "Settings",
							TableEntryReference = $"Header.{header.HeaderName}"
						};
					} else if (settingMetadata is FieldMetadata field) {
						var setting = SettingsManager.GetSettingByName(field.FieldName);

						// Spawn the setting
						var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName).WaitForCompletion();
						var go = Instantiate(settingPrefab, settingsContainer);
						go.GetComponent<ISettingVisual>().SetSetting(field.FieldName);
					}
				}

				// Then we're good!
				break;
			}
		}
	}
}