using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YARG.UI {
	public class MainMenu : MonoBehaviour {
		[SerializeField]
		private UIDocument mainMenuDocument;

		// Temp
		[SerializeField]
		private Canvas songSelect;

		private void Start() {
			SetupMainMenu();
		}

		private void SetupMainMenu() {
			var root = mainMenuDocument.rootVisualElement;

			root.Q<Button>("PlayButton").clicked += ShowSongSelect;
		}

		public void ShowSongSelect() {
			mainMenuDocument.gameObject.SetActive(false);
			songSelect.gameObject.SetActive(true);
		}

		public void ShowMainMenu() {
			mainMenuDocument.gameObject.SetActive(true);
			songSelect.gameObject.SetActive(false);

			// Temp
			SetupMainMenu();
		}
	}
}