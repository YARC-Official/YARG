using UnityEngine;

namespace YARG.UI {
	public class EditPlayers : MonoBehaviour {
		[SerializeField]
		private GameObject playerSectionPrefab;

		[SerializeField]
		private Transform playerContiner;
		[SerializeField]
		private Transform addButton;

		private void OnEnable() {
			UpdatePlayers();
		}

		private void UpdatePlayers() {
			// Delete old (except add button)
			foreach (Transform t in playerContiner) {
				if (t == addButton) {
					continue;
				}

				Destroy(t.gameObject);
			}

			// Create new player sections
			foreach (var player in PlayerManager.players) {
				var go = Instantiate(playerSectionPrefab, playerContiner);
				go.GetComponent<PlayerSection>().SetPlayer(player);
			}

			// Put at end
			addButton.SetAsLastSibling();
		}
	}
}