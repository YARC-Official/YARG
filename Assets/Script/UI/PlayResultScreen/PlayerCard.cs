using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

using YARG.Data;
using YARG.Input;

namespace YARG.UI.PlayResultScreen {
	// TODO: move into more appropriate spot?
    public enum ClearStatus {
        Disqualified, Cleared, FullCombo, Brutal, Bot
    }

    public class PlayerCard : MonoBehaviour {

		public static readonly Dictionary<ClearStatus, Color> statusColor = new() {
            {ClearStatus.Disqualified, new Color(.322f, .345f, .377f)},
            {ClearStatus.Bot, new Color(.322f, .345f, .377f)},
            {ClearStatus.Cleared, new Color(.18f, .85f, 1f)},
            {ClearStatus.FullCombo, new Color(1f, .76f, .16f)},
            {ClearStatus.Brutal, new Color(.82f, 0f, .8f)},
		};

		private PlayerManager.Player player;

		private int curPage = 0;
		private int pageCount = 2;

		[SerializeField]
		private Sprite backgroundDisqualified;
		[SerializeField]
		private Sprite backgroundCleared;
		[SerializeField]
		private Sprite backgroundFullCombo;
		[SerializeField]
		private TextMeshProUGUI bottomBannerText;

		[Space]
		[SerializeField]
		private Image containerImg;
		[SerializeField]
		private Image fcSymbol;
		[SerializeField]
		private TextMeshProUGUI instrumentSymbol;
		[SerializeField]
		private ScrollRect scrollContainer;

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
		private TextMeshProUGUI detailNotesHit;
		[SerializeField]
		private TextMeshProUGUI detailMaxStreak;
		[SerializeField]
		private TextMeshProUGUI detailMissedNotes;
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
			this.player = player;

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
			instrumentSymbol.text = $"<sprite name=\"{player.chosenInstrument}\">";

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

			// detailed combo info
			detailNotesHit.text = $"{scr.notesHit}<color=#ffffff> / {scr.notesHit + scr.notesMissed}";
			detailMaxStreak.text = "TODO"; // TODO
			detailMissedNotes.text = scr.notesMissed.ToString();

			/* Bottom banner */
			// Text
			if (cs == ClearStatus.Bot) {
				bottomBannerText.text = "<color=#848D94>BOT";
			} else if (cs == ClearStatus.Disqualified) {
				bottomBannerText.text = "<color=#848D94>DISQUALIFIED";
			} else if (isHighScore) {
				bottomBannerText.text = "HIGH SCORE";
			} else {
				bottomBannerText.text = String.Empty;
			}
			bottomBanner.gameObject.GetComponent<LayoutElement>().flexibleHeight = 0f;
			
			// DQ Banner
			if (bottomBannerText.text != String.Empty && bottomBannerText.text != "HIGH SCORE") {
				var anim = bottomBanner.gameObject.GetComponent<Animator>();
				anim.enabled = true;
				anim.Play("ExtendBanner", 0, 1f);
			}

			/* Set colors */
			// separators (preserve alpha)
			var c = separator0.color;
			c.r = statusColor[cs].r;
			c.g = statusColor[cs].g;
			c.b = statusColor[cs].b;
			separator0.color = c;
			separator1.color = c;

			// texts' color
			difficulty.color = statusColor[cs];
			detailNotesHit.color = statusColor[cs];
			detailMaxStreak.color = statusColor[cs];
			detailMissedNotes.color = statusColor[cs];

			bottomBanner.color = statusColor[cs];

			// Lower alpha if disqualified
			if (containerImg.sprite == backgroundDisqualified) {
				GetComponent<CanvasGroup>().alpha = 0.4f;
			}
		}

		public void BeginAnimation() {
			// begin tracking player inputs
			player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;

			// only animate if high score
			if (bottomBannerText.text == "HIGH SCORE") {
				var anim = bottomBanner.gameObject.GetComponent<Animator>();
				anim.enabled = true;
				anim.Play("ExtendBanner");
			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool pressed) {
			if (!pressed) return;

			int desiredPage = curPage;

			switch (navigationType) {
				case NavigationType.UP:
					--desiredPage;
					break;
				case NavigationType.DOWN:
					++desiredPage;
					break;
			}

			if (navigationType == NavigationType.UP)
				--desiredPage;
			else if (navigationType == NavigationType.DOWN)
				++desiredPage;

			desiredPage = math.clamp(desiredPage + 1, 0, pageCount - 1);

			// we don't go anywhere
			if (desiredPage == curPage) return;

			// TODO: pages, secstions

			curPage = desiredPage;
		}

		private void OnDisable() {
			// unsubscribe from player inputs
			if (player != null) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
		}
    }
}
