using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Data;
using YARG.Song;

namespace YARG.UI.MusicLibrary {
	public class DifficultyRing : MonoBehaviour {
		[SerializeField]
		private Image instrumentIcon;
		[SerializeField]
		private Image ringSprite;

		[SerializeField]
		private Sprite[] ringSprites;

		public void SetInfo(SongEntry songEntry, Instrument instrument) {
			bool show = songEntry.HasInstrument(instrument);

			SetInfo(show, instrument, songEntry.PartDifficulties.GetValueOrDefault(instrument, -1));
		}

		public void SetInfo(bool hasInstrument, Instrument instrument, int difficulty) {
			SetInfo(hasInstrument, instrument.ToStringName(), difficulty);
		}

		public void SetInfo(bool hasInstrument, string instrumentName, int difficulty) {
			// Set instrument icon
			var icon = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instrumentName}]").WaitForCompletion();
			instrumentIcon.sprite = icon;

			// Acceptable difficulty range is -1 to 6
			if (difficulty < -1) {
				difficulty = 0; // Clamp values below -1 to 0 since this is a specifically-set value
			} else if (difficulty > 6) {
				difficulty = 6;
			}

			if (!hasInstrument) {
				difficulty = -1;
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