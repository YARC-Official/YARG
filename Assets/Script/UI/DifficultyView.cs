using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace YARG.UI {
	public class DifficultyView : MonoBehaviour {
		[SerializeField]
		private Image instrumentIcon;
		[SerializeField]
		private Image ringSprite;

		[SerializeField]
		private Sprite[] ringSprites;

		public void SetInfo(string instrument, int difficulty) {
			// Set instrument icon
			var icon = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instrument}]").WaitForCompletion();
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
				color.a = 0.25f;
			}
			instrumentIcon.color = color;
		}
	}
}