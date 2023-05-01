using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Metadata;
using YARG.Settings.Visuals;
using YARG.UI;
using YARG.Util;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private GameObject fullContainer;
		[SerializeField]
		private GameObject halfContainer;
		[SerializeField]
		private Transform previewContainer;

		[Space]
		[SerializeField]
		private Transform tabsContainer;
		[SerializeField]
		private Transform settingsContainer;

		[Space]
		[SerializeField]
		private Transform halfSettingsContainer;

		[Space]
		[SerializeField]
		private GameObject tabPrefab;
		[SerializeField]
		private GameObject buttonPrefab;
		[SerializeField]
		private GameObject headerPrefab;
		[SerializeField]
		private GameObject directoryPrefab;

		[Space]
		[SerializeField]
		private RenderTexture previewRenderTexture;
		[SerializeField]
		private RawImage previewRawImage;

		private string _currentTab;
		public string CurrentTab {
			get => _currentTab;
			set {
				_currentTab = value;

				UpdateSettingsForTab();
			}
		}

		public bool hasSongLibraryChanged = false;

		private void OnEnable() {
			ReturnToFirstTab();
			UpdateTabs();
		}

		private void OnDisable() {
			DestroyPreview();

			// Save on close
			SettingsManager.SaveSettings();

			if (hasSongLibraryChanged) {
				// Refresh
				MainMenu.Instance.RefreshSongLibrary();

				hasSongLibraryChanged = false;
			}
		}

		private void UpdateSettingsForTab() {
			if (CurrentTab == "_SongFolderManager") {
				UpdateSongFolderManager();

				return;
			}

			var tabInfo = SettingsManager.GetTabByName(CurrentTab);

			if (string.IsNullOrEmpty(tabInfo.previewPath)) {
				if (!fullContainer.activeSelf) {
					fullContainer.gameObject.SetActive(true);
					halfContainer.gameObject.SetActive(false);

					UpdateTabs();
				}

				UpdateSettings(settingsContainer);
			} else {
				if (!halfContainer.activeSelf) {
					halfContainer.gameObject.SetActive(true);
					fullContainer.gameObject.SetActive(false);
				}

				UpdateSettings(halfSettingsContainer);
			}

			UpdatePreview(tabInfo);
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

		private void UpdateSettings(Transform container) {
			// Destroy all previous settings
			foreach (Transform t in container) {
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
						var go = Instantiate(buttonPrefab, container);
						go.GetComponent<SettingsButton>().SetInfo(buttonRow.Buttons[0]);
					} else if (settingMetadata is HeaderMetadata header) {
						// Spawn in the header
						SpawnHeader(container, $"Header.{header.HeaderName}");
					} else if (settingMetadata is FieldMetadata field) {
						var setting = SettingsManager.GetSettingByName(field.FieldName);

						// Spawn the setting
						var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName).WaitForCompletion();
						var go = Instantiate(settingPrefab, container);
						go.GetComponent<ISettingVisual>().SetSetting(field.FieldName);
					}
				}

				// Then we're good!
				break;
			}
		}

		public void UpdateSongFolderManager() {
			hasSongLibraryChanged = true;

			// Destroy all previous settings
			foreach (Transform t in settingsContainer) {
				Destroy(t.gameObject);
			}

			// Spawn header
			SpawnHeader(settingsContainer, "Header.Cache");

			// Spawn refresh all button
			{
				var go = Instantiate(buttonPrefab, settingsContainer);
				go.GetComponent<SettingsButton>().SetCustomCallback(() => {
					hasSongLibraryChanged = false;

					if (Directory.Exists(SongLibrary.CacheFolder)) {
						// Delete cache folder
						Directory.Delete(SongLibrary.CacheFolder, true);

						// Refresh
						MainMenu.Instance.RefreshSongLibrary();
					}
				}, "RefreshAllCaches");
			}

			// Spawn header
			SpawnHeader(settingsContainer, "Header.SongFolders");

			// Spawn add folder button
			{
				var go = Instantiate(buttonPrefab, settingsContainer);
				go.GetComponent<SettingsButton>().SetCustomCallback(() => {
					// Use a list to add the new folder to the end
					Array.Resize(ref SettingsManager.Settings.SongFolders,
						SongLibrary.SongFolders.Length + 1);

					// Refresh everything
					UpdateSongFolderManager();
				}, "AddFolder");
			}

			// Create all of the directories
			for (int i = 0; i < SongLibrary.SongFolders.Length; i++) {
				var path = SongLibrary.SongFolders[i];

				var go = Instantiate(directoryPrefab, settingsContainer);
				go.GetComponent<SettingsDirectory>().SetIndex(i, false);
			}

			// Spawn header
			SpawnHeader(settingsContainer, "Header.SongUpgrades");

			// Spawn add upgrade folder button
			{
				var go = Instantiate(buttonPrefab, settingsContainer);
				go.GetComponent<SettingsButton>().SetCustomCallback(() => {
					// Use a list to add the new folder to the end
					Array.Resize(ref SettingsManager.Settings.SongUpgradeFolders,
						SongLibrary.SongUpgradeFolders.Length + 1);

					// Refresh everything
					UpdateSongFolderManager();
				}, "AddUpgradeFolder");
			}

			// Create all of the song upgrade directories
			for (int i = 0; i < SongLibrary.SongUpgradeFolders.Length; i++) {
				var path = SongLibrary.SongUpgradeFolders[i];

				var go = Instantiate(directoryPrefab, settingsContainer);
				go.GetComponent<SettingsDirectory>().SetIndex(i, true);
			}
		}

		private void SpawnHeader(Transform container, string localizationKey) {
			// Spawn the header
			var go = Instantiate(headerPrefab, container);

			// Set header text
			go.GetComponentInChildren<LocalizeStringEvent>().StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = localizationKey
			};
		}

		private void UpdatePreview(SettingsManager.Tab tabInfo) {
			DestroyPreview();

			if (string.IsNullOrEmpty(tabInfo.previewPath)) {
				return;
			}

			// Spawn prefab
			var previewPrefab = Addressables.LoadAssetAsync<GameObject>(tabInfo.previewPath).WaitForCompletion();
			Instantiate(previewPrefab, previewContainer);

			// Set render texture
			CameraPreviewTexture.SetAllPreviews();

			// Size raw image
			previewRawImage.texture = CameraPreviewTexture.PreviewTexture;
			previewRawImage.uvRect = previewRawImage.rectTransform.ToViewportSpaceCentered(v: false);
		}

		private void DestroyPreview() {
			foreach (Transform t in previewContainer) {
				Destroy(t.gameObject);
			}
		}

		public void ReturnToFirstTab() {
			// Select the first tab
			foreach (var tab in SettingsManager.SETTINGS_TABS) {
				// Skip tabs that aren't shown in game, if we are in game
				if (!tab.showInGame && GameManager.Instance.CurrentScene == SceneIndex.PLAY) {
					continue;
				}

				CurrentTab = tab.name;
				break;
			}
		}

		public void ForceShowCurrentTabInFull() {
			if (fullContainer.activeSelf) {
				return;
			}

			fullContainer.gameObject.SetActive(true);
			halfContainer.gameObject.SetActive(false);

			UpdateTabs();
			UpdateSettings(settingsContainer);
		}
	}
}