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
			// Mic doesn't have a lefty flip
			leftyFlipToggle.interactable =
				player.inputStrategy is not RealGuitarInputStrategy &&
				player.inputStrategy is not MicInputStrategy;

			// Mic doesn't have a track speed
			trackSpeedField.interactable =
				player.inputStrategy is not MicInputStrategy;
		}

		public void DeletePlayer() {
			PlayerManager.players.Remove(player);
			player.inputStrategy.Disable();
			player = null;
			Destroy(gameObject);
			PlayBackSoundEffect();		
		}

		public void UpdateTrackSpeed() {
			if (player != null) {
				player.trackSpeed = float.Parse(trackSpeedField.text, CultureInfo.InvariantCulture);
				PlayMenuNavigationSoundEffect();
			}
		}

		public void UpdateLeftyFlip() {
			if (player != null) {
				player.leftyFlip = leftyFlipToggle.isOn;
				if (player.leftyFlip) {
					PlaySelectSoundEffect();
				} else {
					PlayBackSoundEffect();
				}
			}
		}

		public void PlaySelectSoundEffect() {
			GameManager.AudioManager.PlaySoundEffect(AudioManager.Instance.SelectSfx);
		}

		private void PlayBackSoundEffect() {
			GameManager.AudioManager.PlaySoundEffect(AudioManager.Instance.SelectSfx);
		}
		
		private void PlayMenuNavigationSoundEffect() {
			GameManager.AudioManager.PlaySoundEffect(SfxSample.MenuNavigation);
		}
	}
}