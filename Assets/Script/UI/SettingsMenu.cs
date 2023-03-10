using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private TMP_InputField songFolderInput;
		[SerializeField]
		private TMP_InputField calibrationInput;

		private void Start() {
			songFolderInput.text = SongLibrary.songFolder.FullName;
			calibrationInput.text = PlayerManager.globalCalibration.ToString();
		}

		public void SongFolderUpdate() {
			SongLibrary.songFolder = new(songFolderInput.text);
			PlayerPrefs.SetString("songFolder", songFolderInput.text);

			SongLibrary.Reset();
		}

		public void CalibrationUpdate() {
			// Guaranteed to as the input field is decimal
			PlayerManager.globalCalibration = float.Parse(calibrationInput.text);
		}
	}
}