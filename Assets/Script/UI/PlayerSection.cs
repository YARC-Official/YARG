using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.UI {
	public class PlayerSection : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI playerName;
		[SerializeField]
		private TMP_InputField trackSpeedField;
		[SerializeField]
		private Toggle leftyFlipToggle;

		private PlayerManager.Player player;

		public void SetPlayer(PlayerManager.Player player) {
			this.player = player;

			playerName.text = player.DisplayName;
			trackSpeedField.text = player.trackSpeed.ToString("N1", CultureInfo.InvariantCulture);
			leftyFlipToggle.isOn = player.leftyFlip;

			// Pro-guitar lefty flip is a little bit more complicated (TODO)
			leftyFlipToggle.interactable = player.inputStrategy is not RealGuitarInputStrategy;
		}

		public void DeletePlayer() {
			PlayerManager.players.Remove(player);
			Destroy(gameObject);
		}

		public void UpdateTrackSpeed() {
			if (player != null) {
				player.trackSpeed = float.Parse(trackSpeedField.text, CultureInfo.InvariantCulture);
			}
		}

		public void UpdateLeftyFlip() {
			if (player != null) {
				player.leftyFlip = leftyFlipToggle.isOn;
			}
		}
	}
}