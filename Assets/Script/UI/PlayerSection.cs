using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class PlayerSection : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI playerName;

		private PlayerManager.Player player;

		public void SetPlayer(PlayerManager.Player player) {
			this.player = player;

			playerName.text = player.DisplayName;
		}

		public void DeletePlayer() {
			PlayerManager.players.Remove(player);
			Destroy(gameObject);
		}
	}
}