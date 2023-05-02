using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Data;

namespace YARG.UI.MusicLibrary {
	public class DifficultyRing : MonoBehaviour {
		[SerializeField]
		private Image instrumentIcon;
		[SerializeField]
		private Image ringSprite;

		[SerializeField]
		private Sprite[] ringSprites;

		public void SetInfo(Dictionary<Instrument, int> difficulties, Instrument instrument) {
			SetInfo(instrument, difficulties[instrument]);
		}

		public void SetInfo(Instrument instrument, int difficulty) {
			// Set instrument icon
			var icon = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instrument.ToStringName()}]").WaitForCompletion();
			instrumentIcon.sprite = icon;

			// Acceptable difficulty range is -1 to 6
			if (difficulty < -1) {
				difficulty = 0; // Clamp values below -1 to 0 since this is a specifically-set value
			} else if (difficulty > 6) {
				difficulty = 6;
			}

			// Set ring sprite
			int index = difficulty + 1;
			ringSprite.sprite = ringSprites[index];

			// Set instrument opacity
			Color color = instrumentIcon.color;
			color.a = 1f;
			if (difficulty == -1) {
				color.a = 0.2f;
			}
			instrumentIcon.color = color;
		}
	}
}