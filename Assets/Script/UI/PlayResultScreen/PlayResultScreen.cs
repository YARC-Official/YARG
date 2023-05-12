using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using DG.Tweening;

using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI.PlayResultScreen {
    public class PlayResultScreen : MonoBehaviour {
		private readonly Color PASS = new(.18f, .85f, 1f);
		private readonly Color FAIL = new(.95f, .17f, .22f);
		private readonly Color PASS_TRANSLUCENT = new(.18f, .85f, 1f, .2f);
		private readonly Color FAIL_TRANSLUCENT = new(.95f, .17f, .22f, .2f);

		[SerializeField]
		private GameObject playerCardPrefab;
		[SerializeField]
		private GameObject playerCardsContainer;

		[Space]
		// [SerializeField]
		// private RawImage headerBorder;
		[SerializeField]
		private Image backgroundBorderPass;
		[SerializeField]
		private Image backgroundBorderFail;
		[SerializeField]
		private RawImage headerBackgroundPassed;

		[SerializeField]
		private bool hasFailed;

		[Space]
		[SerializeField]
		private RectTransform marginContainerRT;
		[SerializeField]
		private TextMeshProUGUI songTitle;
        [SerializeField]
		private TextMeshProUGUI songArtist;
		[SerializeField]
		private CanvasGroup starScoreCG;
		[SerializeField]
		private StarDisplay starDisplay;
		[SerializeField]
		private TextMeshProUGUI score;
		[SerializeField]
		private CanvasGroup helpBarCG;

		private List<PlayerCard> playerCards = new();

		public HashSet<PlayerManager.Player> highScores;
		public HashSet<PlayerManager.Player> disqualified;
		public HashSet<PlayerManager.Player> bot;

		void OnEnable() {
			// Populate header information
			songTitle.SetText(GameManager.Instance?.SelectedSong.Name);
			songArtist.SetText(GameManager.Instance?.SelectedSong?.Artist);
			score.SetText(ScoreKeeper.TotalScore.ToString("n0"));

			int stars = (int)StarScoreKeeper.BandStars;
			Debug.Log($"BandStars: {stars}");
			starDisplay.SetStars(stars, stars <= 5 ? StarType.Standard : StarType.Gold);

			// change graphics depending on clear/fail
			backgroundBorderFail.gameObject.SetActive(hasFailed);
            backgroundBorderPass.gameObject.SetActive(!hasFailed);
			headerBackgroundPassed.gameObject.SetActive(!hasFailed);
			songArtist.color = hasFailed ? FAIL : PASS;

			ProcessScores();
			CreatePlayerCards();

			StartCoroutine(EnableAnimation());
		}

		IEnumerator EnableAnimation() {
			/* Initial States */
			// star score
			starScoreCG.alpha = 0f;

			// margin container (player cards)
			var ccYMinTgt = marginContainerRT.anchorMin.y;
			var ccYMaxTgt = marginContainerRT.anchorMax.y;
			marginContainerRT.anchorMin += new Vector2(0, 1);
			marginContainerRT.anchorMax += new Vector2(0, 1);

			// help bar
			helpBarCG.alpha = 0;

			/* Run Animations */
			// fade in background
			yield return backgroundBorderPass
				.DOFade(0, 1.5f)
				.From()
				.WaitForCompletion();

			// fade in score stars
			yield return starScoreCG
				.DOFade(1f, 0.5f)
				.WaitForCompletion();

			// slide in player cards
			marginContainerRT
				.DOAnchorMin(new Vector2(marginContainerRT.anchorMin.x, ccYMinTgt), .75f)
				.SetEase(Ease.OutBack, overshoot: 1.2f);
			yield return marginContainerRT
				.DOAnchorMax(new Vector2(marginContainerRT.anchorMax.x, ccYMaxTgt), .75f)
				.SetEase(Ease.OutBack, overshoot: 1.2f)
				.WaitForCompletion();


			OnEnableAnimationFinish();
			
			// fade in helpbar
			helpBarCG.DOFade(1f, .5f);
		}

		/// <summary>
		/// Populate relevant score data; save scores.
		/// </summary>
		private void ProcessScores() {
			// Create a score to push
			var songScore = new SongScore {
				lastPlayed = DateTime.Now,
				timesPlayed = 1,
				highestPercent = new(),
				highestScore = new()
			};
			var oldScore = ScoreManager.GetScore(GameManager.Instance?.SelectedSong);

			highScores = new();
			disqualified = new();
			bot = new();
			foreach (var player in PlayerManager.players) {
				// Skip "Sit Out"s
				if (player.chosenInstrument == null) {
					continue;
				}

				// Bots
				if (player.inputStrategy.botMode) {
					bot.Add(player);
					continue;
				}

				// DQ non-100% speeds
				if (Play.speed != 1f) {
					disqualified.Add(player);
					continue;
				}

				// DQ no scores
				if (!player.lastScore.HasValue) {
					disqualified.Add(player);
					continue;
				}

				var lastScore = player.lastScore.GetValueOrDefault();

				// Skip if the chart has no notes
				if (lastScore.notesHit + lastScore.notesMissed == 0) {
					disqualified.Add(player);
					continue;
				}

				// Override or add score/percentage
				// TODO: override scores/percentages independently
				if (oldScore == null || oldScore.highestScore == null ||
					!oldScore.highestScore.TryGetValue(player.chosenInstrument, out var oldHighestSc) ||
					lastScore.score > oldHighestSc) {
						
					songScore.highestPercent[player.chosenInstrument] = lastScore.percentage;
					songScore.highestScore[player.chosenInstrument] = lastScore.score;
					highScores.Add(player);
				}
			}

			ScoreManager.PushScore(GameManager.Instance?.SelectedSong, songScore);
		}

		/// <summary>
		/// Instantiate player cards to display.
		/// </summary>
		private void CreatePlayerCards() {
			// clear existing cards (may be left in for dev preview)
			foreach (Transform pc in playerCardsContainer.transform) {
				Destroy(pc.gameObject);
			}
			playerCards.Clear();

			foreach (var player in PlayerManager.players) {
				// skip players sitting out
				if (player.chosenInstrument == null) continue;

				var pc = Instantiate(playerCardPrefab, playerCardsContainer.transform).GetComponent<PlayerCard>();
				
				ClearStatus clr;
				if (bot.Contains(player)) {
					clr = ClearStatus.Bot;
				} else if (disqualified.Contains(player)) {
					clr = ClearStatus.Disqualified;
				} else {
					clr = ClearStatus.Cleared;
				}

				pc.Setup(player, clr, highScores.Contains(player));
				playerCards.Add(pc);
			}
		}

		private void OnEnableAnimationFinish() {
			// Subscribe to player inputs
			foreach (var p in PlayerManager.players) {
				p.inputStrategy.GenericNavigationEvent += OnGenericNavigation;
			}
			foreach (var pc in playerCards) {
				pc.Engage();
			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool pressed) {
			if (!pressed) return;

			switch (navigationType) {
				case NavigationType.PRIMARY:
					PlayExit();
					break;
				case NavigationType.TERTIARY:
					PlayRestart();
					break;
			}
		}

		// TODO: replace with common restart call (ie. what the pause menu calls)
		public void PlayRestart() {
			GameManager.AudioManager.UnloadSong();
			GameManager.Instance.LoadScene(SceneIndex.PLAY);
			Play.Instance.Paused = false;
		}

		/// <summary>
		/// Go to song select.
		/// </summary>
		public void PlayExit() {
			Play.Instance.Exit();
		}

		private void OnDisable() {
			// Unsubscribe player inputs
			try {
				foreach (var p in PlayerManager.players) {
					p.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
				}
			} catch {}
		}
    }
}
