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

			// Set ring sprite
			int index = difficulty + 1;
			if (difficulty == -2) {
				index = 1;
			}
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