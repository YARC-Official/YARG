using UnityEngine;
using UnityEngine.UIElements;
using YARG.Utils;

namespace YARG.UI {
	public partial class MainMenu : MonoBehaviour {
		[SerializeField]
		private UIDocument mainMenuDocument;
		[SerializeField]
		private UIDocument editPlayersDocument;

		// Temp
		[SerializeField]
		private Canvas songSelect;

		private void Start() {
			SetupMainMenu();
			SetupEditPlayers();

			ShowMainMenu();
		}

		private void Update() {
			UpdateInputWaiting();
		}

		private void SetupMainMenu() {
			var root = mainMenuDocument.rootVisualElement;

			root.Q<Button>("PlayButton").clicked += ShowSongSelect;
			root.Q<Button>("EditPlayersButton").clicked += ShowEditPlayers;
		}

		public void ShowMainMenu() {
			mainMenuDocument.SetVisible(true);
			editPlayersDocument.SetVisible(false);
			songSelect.gameObject.SetActive(false);
		}

		public void ShowEditPlayers() {
			mainMenuDocument.SetVisible(false);
			editPlayersDocument.SetVisible(true);
			songSelect.gameObject.SetActive(false);
		}

		public void ShowSongSelect() {
			mainMenuDocument.SetVisible(false);
			editPlayersDocument.SetVisible(false);
			songSelect.gameObject.SetActive(true);
		}
	}
}