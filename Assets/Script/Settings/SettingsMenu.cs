using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Input;
using YARG.Settings.Metadata;
using YARG.Settings.Visuals;
using YARG.UI.MusicLibrary;
using YARG.Util;

namespace YARG.Settings {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private GameObject _fullContainer;

		[SerializeField]
		private GameObject _halfContainer;

		[SerializeField]
		private Transform _previewContainer;

		[Space]
		[SerializeField]
		private Transform _tabsContainer;

		[SerializeField]
		private Transform _settingsContainer;

		[Space]
		[SerializeField]
		private Transform _halfSettingsContainer;

		[Space]
		[SerializeField]
		private GameObject _tabPrefab;

		[SerializeField]
		private GameObject _buttonPrefab;

		[SerializeField]
		private GameObject _headerPrefab;

		[SerializeField]
		private GameObject _directoryPrefab;

		[Space]
		[SerializeField]
		private RawImage _previewRawImage;

		private string _currentTab;

		public string CurrentTab {
			get => _currentTab;
			set {
				_currentTab = value;

				UpdateSettingsForTab();
			}
		}

		public bool UpdateSongLibraryOnExit { get; set; } = false;

		private void OnEnable() {
			// Set navigation scheme
			Navigator.Instance.PushScheme(new NavigationScheme(new() {
				new NavigationScheme.Entry(MenuAction.Back, "Back", () => { gameObject.SetActive(false); })
			}, true));

			ReturnToFirstTab();
			UpdateTabs();
		}

		private async UniTask OnDisable() {
			Navigator.Instance.PopScheme();

			DestroyPreview();

			// Save on close
			SettingsManager.SaveSettings();

			if (UpdateSongLibraryOnExit) {
				UpdateSongLibraryOnExit = false;

				// Do a song refresh if requested
				LoadingManager.Instance.QueueSongRefresh(true);
				await LoadingManager.Instance.StartLoad();

				// Then refresh song select
				SongSelection.refreshFlag = true;
			}
		}

		public void UpdateSettingsForTab() {
			if (CurrentTab == "_SongFolderManager") {
				UpdateSongFolderManager();

				return;
			}

			var tabInfo = SettingsManager.GetTabByName(CurrentTab);

			if (string.IsNullOrEmpty(tabInfo.PreviewPath)) {
				if (!_fullContainer.activeSelf) {
					_fullContainer.gameObject.SetActive(true);
					_halfContainer.gameObject.SetActive(false);

					UpdateTabs();
				}

				UpdateSettings(_settingsContainer);
			} else {
				if (!_halfContainer.activeSelf) {
					_halfContainer.gameObject.SetActive(true);
					_fullContainer.gameObject.SetActive(false);
				}

				UpdateSettings(_halfSettingsContainer);
			}

			UpdatePreview(tabInfo);
		}

		private void UpdateTabs() {
			// Destroy all previous tabs
			foreach (Transform t in _tabsContainer) {
				Destroy(t.gameObject);
			}

			// Then, create new tabs!
			foreach (var tab in SettingsManager.SettingsTabs) {
				// Skip tabs that aren't shown in game, if we are in game
				if (!tab.ShowInPlayMode && GameManager.Instance.CurrentScene == SceneIndex.PLAY) {
					continue;
				}

				var go = Instantiate(_tabPrefab, _tabsContainer);
				go.GetComponent<SettingsTab>().SetTab(tab.Name, tab.Icon);
			}
		}

		private void UpdateSettings(Transform container) {
			// Destroy all previous settings
			foreach (Transform t in container) {
				Destroy(t.gameObject);
			}

			foreach (var tab in SettingsManager.SettingsTabs) {
				// Look for the tab
				if (tab.Name != CurrentTab) {
					continue;
				}

				// Once we've found the tab, add the settings
				foreach (var settingMetadata in tab.Settings) {
					if (settingMetadata is ButtonRowMetadata buttonRow) {
						// Spawn the button
						var go = Instantiate(_buttonPrefab, container);
						go.GetComponent<SettingsButton>().SetInfo(buttonRow.Buttons);
					} else if (settingMetadata is HeaderMetadata header) {
						// Spawn in the header
						SpawnHeader(container, $"Header.{header.HeaderName}");
					} else if (settingMetadata is FieldMetadata field) {
						var setting = SettingsManager.GetSettingByName(field.FieldName);

						// Spawn the setting
						var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName)
							.WaitForCompletion();
						var go = Instantiate(settingPrefab, container);
						go.GetComponent<ISettingVisual>().SetSetting(field.FieldName);
					}
				}

				// Then we're good!
				break;
			}
		}

		public void UpdateSongFolderManager() {
			UpdateSongLibraryOnExit = true;

			// Destroy all previous settings
			foreach (Transform t in _settingsContainer) {
				Destroy(t.gameObject);
			}

			// Spawn header
			SpawnHeader(_settingsContainer, "Header.Cache");

			// Spawn refresh all button
			{
				var go = Instantiate(_buttonPrefab, _settingsContainer);
				go.GetComponent<SettingsButton>().SetCustomCallback(async () => {
					LoadingManager.Instance.QueueSongRefresh(false);
					await LoadingManager.Instance.StartLoad();
				}, "RefreshAllCaches");
			}

			// Spawn header
			SpawnHeader(_settingsContainer, "Header.SongFolders");

			// Spawn add folder button
			{
				var go = Instantiate(_buttonPrefab, _settingsContainer);
				go.GetComponent<SettingsButton>().SetCustomCallback(() => {
					SettingsManager.Settings.SongFolders.Add(string.Empty);

					// Refresh everything
					UpdateSongFolderManager();
				}, "AddFolder");
			}

			// Create all of the directories
			for (int i = 0; i < SettingsManager.Settings.SongFolders.Count; i++) {
				var go = Instantiate(_directoryPrefab, _settingsContainer);
				go.GetComponent<SettingsDirectory>().SetIndex(i, false);
			}
		}

		private void SpawnHeader(Transform container, string localizationKey) {
			// Spawn the header
			var go = Instantiate(_headerPrefab, container);

			// Set header text
			go.GetComponentInChildren<LocalizeStringEvent>().StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = localizationKey
			};
		}

		private void UpdatePreview(SettingsManager.Tab tabInfo) {
			DestroyPreview();

			if (string.IsNullOrEmpty(tabInfo.PreviewPath)) {
				return;
			}

			// Spawn prefab
			var previewPrefab = Addressables.LoadAssetAsync<GameObject>(tabInfo.PreviewPath).WaitForCompletion();
			Instantiate(previewPrefab, _previewContainer);

			// Set render texture
			CameraPreviewTexture.SetAllPreviews();

			// Size raw image
			_previewRawImage.texture = CameraPreviewTexture.PreviewTexture;
			var rect = _previewRawImage.rectTransform.ToViewportSpaceCentered(v: false);
			rect.y = 0f;
			_previewRawImage.uvRect = rect;
		}

		private void DestroyPreview() {
			if (_previewContainer == null)
				return;

			foreach (Transform t in _previewContainer) {
				Destroy(t.gameObject);
			}
		}

		public void ReturnToFirstTab() {
			// Select the first tab
			foreach (var tab in SettingsManager.SettingsTabs) {
				// Skip tabs that aren't shown in game, if we are in game
				if (!tab.ShowInPlayMode && GameManager.Instance.CurrentScene == SceneIndex.PLAY) {
					continue;
				}

				CurrentTab = tab.Name;
				break;
			}
		}

		public void ForceShowCurrentTabInFull() {
			if (_fullContainer.activeSelf) {
				return;
			}

			_fullContainer.gameObject.SetActive(true);
			_halfContainer.gameObject.SetActive(false);

			UpdateTabs();
			UpdateSettings(_settingsContainer);
		}
	}
}