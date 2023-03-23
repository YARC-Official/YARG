using System.Globalization;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Serialization;

namespace YARG.UI {
	public class SettingsMenu : MonoBehaviour {
		public static SettingsMenu Instance {
			get;
			private set;
		}

		[SerializeField]
		private TMP_InputField songFolderInput;
		[SerializeField]
		private TMP_InputField calibrationInput;
		[SerializeField]
		private Toggle lowQualityToggle;
		[SerializeField]
		private Toggle karaokeToggle;
		[SerializeField]
		private Toggle showHitWindowToggle;
		[SerializeField]
		private Toggle useAudioTimeToggle;
		[SerializeField]
		private TMP_InputField ipInput;

		[SerializeField]
		private GameObject joinServerButton;

		private void Start() {
			Instance = this;

			songFolderInput.text = SongLibrary.songFolder.FullName;
			calibrationInput.text = PlayerManager.globalCalibration.ToString(CultureInfo.InvariantCulture);
			lowQualityToggle.isOn = GameManager.Instance.LowQualityMode;
			karaokeToggle.isOn = GameManager.Instance.KaraokeMode;
			showHitWindowToggle.isOn = GameManager.Instance.showHitWindow;
			useAudioTimeToggle.isOn = GameManager.Instance.useAudioTime;

			if (GameManager.client != null) {
				joinServerButton.SetActive(false);
			}
		}

		public void BrowseSongFolder() {
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", null, false, folder => {
				songFolderInput.text = folder[0];
				SongFolderUpdate();
			});
		}

		public void SongFolderUpdate() {
			if (GameManager.client == null) {
				SongLibrary.songFolder = new(songFolderInput.text);
				PlayerPrefs.SetString("songFolder", songFolderInput.text);
				MainMenu.Instance.RefreshSongLibrary();
			}
		}

		public void SongFolderForceUpdate() {
			songFolderInput.text = SongLibrary.songFolder?.FullName;
		}

		public void OuvertExportButton() {
			StandaloneFileBrowser.SaveFilePanelAsync("Save Song List", null, "songs", "json", path => {
				OuvertExport.ExportOuvertSongsTo(path);
			});
		}

		public void CalibrationUpdate() {
			// Guaranteed to as the input field is decimal
			PlayerManager.globalCalibration = float.Parse(calibrationInput.text, CultureInfo.InvariantCulture);
		}

		public void LowQualityUpdate() {
			GameManager.Instance.LowQualityMode = lowQualityToggle.isOn;
		}

		public void KaraokeModeUpdate() {
			GameManager.Instance.KaraokeMode = karaokeToggle.isOn;
		}

		public void ShowHitWindowUpdate() {
			GameManager.Instance.showHitWindow = showHitWindowToggle.isOn;
		}

		public void AudioTimeUpdate() {
			GameManager.Instance.useAudioTime = useAudioTimeToggle.isOn;
		}

		public void JoinServer() {
			GameManager.client = new();
			GameManager.client.Start(ipInput.text);

			joinServerButton.SetActive(false);
		}
	}
}