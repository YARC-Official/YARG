using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using YARG.Data;

namespace YARG.UI.PlayResultScreen {
    public enum ClearStatus {
        Disqualified, Cleared, FullCombo, Brutal, Bot
    }

    public class PlayerCard : MonoBehaviour {

		public static readonly Dictionary<ClearStatus, Color> statusColor = new() {
            {ClearStatus.Disqualified, new Color(.16f, .18f, .21f)},
            {ClearStatus.Cleared, new Color(.18f, .85f, 1f)},
            {ClearStatus.FullCombo, new Color(1f, .76f, .16f)},
            {ClearStatus.Brutal, new Color(.82f, 0f, .8f)},
		};

		[SerializeField]
		private Sprite backgroundDisqualified;
		[SerializeField]
		private Sprite backgroundCleared;
		[SerializeField]
		private Sprite backgroundFullCombo;

		[Space]
		[SerializeField]
		private Image containerImg;
		[SerializeField]
		private Image fcSymbol;
		[SerializeField]
		private Image instrumentSymbol;

		[Space]
        [Header("Main Page")]
        [SerializeField]
		private TextMeshProUGUI playerName;
        [SerializeField]
		private TextMeshProUGUI percentage;
        [SerializeField]
		private TextMeshProUGUI difficulty;
		[SerializeField]
		private TextMeshProUGUI score;
		[SerializeField]
		private StarDisplay starDisplay;

		[Space]
		[Header("Color Elements")]
		[SerializeField]
		private RawImage separator0;
        [SerializeField]
		private RawImage separator1;
        [SerializeField]
		private RawImage bottomBanner;

		public void Setup(PlayerManager.Player player, ClearStatus cs, bool isHighScore) {
			Debug.Log($"Setting up PlayerCard for {player.DisplayName}");

			// set window frame
			containerImg.sprite = cs switch {
                ClearStatus.Bot or ClearStatus.Disqualified =>
					backgroundDisqualified,
                ClearStatus.FullCombo => backgroundFullCombo,
                _ => backgroundCleared
			};

            /* Set Content */
			fcSymbol.gameObject.SetActive(cs == ClearStatus.FullCombo);
			playerName.text = player.DisplayName;
            difficulty.text = player.chosenDifficulty switch {
				Difficulty.EXPERT_PLUS => "EXPERT+",
				_ => player.chosenDifficulty.ToString()
			};

			// if (!player.lastScore.HasValue) {
			// 	return;
			// }

            var scr = player.lastScore.Value;
			percentage.text = $"{Mathf.FloorToInt(scr.percentage.percent * 100f)}%";
			score.text = $"{scr.score.score:N0}";
			starDisplay.SetStars (
				scr.score.stars,
				scr.score.stars <= 5 ? StarType.Standard : StarType.Gold
			);

			/* Set colors */
			// separator colors
			var c = separator0.color;
			c.r = statusColor[cs].r;
			c.g = statusColor[cs].g;
			c.b = statusColor[cs].b;
			separator0.color = c;
			separator1.color = c;

			difficulty.color = statusColor[cs];
			bottomBanner.color = statusColor[cs];

			/* Bottom banner */
			// TODO: setup for other clear types
			bottomBanner.gameObject.GetComponent<LayoutElement>().flexibleHeight = 0f;
			if (isHighScore) {
				// animate banner expanding
				var anim = bottomBanner.gameObject.GetComponent<Animator>();
				anim.enabled = true;
				anim.Play("ExtendBanner");
			}
		}
    }
}
