using System.Globalization;
using TMPro;
using UnityEngine;

namespace YARG.UI {
	public class PlayerSection : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI playerName;
		[SerializeField]
		private TMP_InputField trackSpeedField;

		private PlayerManager.Player player;

		public void SetPlayer(PlayerManager.Player player) {
			this.player = player;

			playerName.text = player.DisplayName;
			trackSpeedField.text = player.trackSpeed.ToString("N1", CultureInfo.InvariantCulture);
		}

		public void DeletePlayer() {
			PlayerManager.players.Remove(player);
			Destroy(gameObject);
		}

		public void UpdateTrackSpeed() {
			player.trackSpeed = float.Parse(trackSpeedField.text, CultureInfo.InvariantCulture);
		}
	}
}