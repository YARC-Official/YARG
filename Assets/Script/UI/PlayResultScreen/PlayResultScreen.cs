using System.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using YARG.Data;
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
		[SerializeField]
		private RawImage headerBorder;
		[SerializeField]
		private Image backgroundBorderPass;
		[SerializeField]
		private Image backgroundBorderFail;

		[SerializeField]
		private bool hasFailed;

		[Space]
        [SerializeField]
		private TextMeshProUGUI songTitle;
        [SerializeField]
		private TextMeshProUGUI songArtist;
		[SerializeField]
		private TextMeshProUGUI score;

		public HashSet<PlayerManager.Player> highScores;
		public HashSet<PlayerManager.Player> disqualified;

		void Awake() {
            
        }

		void OnEnable() {
			// Populate header information
			songTitle.SetText(Play.song?.Name);
			songArtist.SetText(Play.song?.Artist);
			score.SetText(ScoreKeeper.TotalScore.ToString("n0"));

			SaveScores();
			CreatePlayerCards();
		}

		private void CreatePlayerCards() {
			foreach (var player in PlayerManager.players) {
				var pc = Instantiate(playerCardPrefab, playerCardsContainer.transform);
				pc.GetComponent<PlayerCard>().Setup(player, ClearStatus.Cleared);
			}
		}

		private void SaveScores() {
			// Create a score to push
			var songScore = new SongScore {
				lastPlayed = DateTime.Now,
				timesPlayed = 1,
				highestPercent = new(),
				highestScore = new()
			};
			var oldScore = ScoreManager.GetScore(Play.song);

			highScores = new();
			disqualified = new();
			foreach (var player in PlayerManager.players) {
				// Skip "Sit Out"s
				if (player.chosenInstrument == null) {
					continue;
				}

				// DQ non-100% speeds
				if (Play.speed != 1f) {
					disqualified.Add(player);
					continue;
				}

				// DQ bots
				if (player.inputStrategy.botMode) {
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

			// Push!
			ScoreManager.PushScore(Play.song, songScore);
		}

        // Update is called once per frame
        void Update() {
            backgroundBorderFail.gameObject.SetActive(hasFailed);
            backgroundBorderPass.gameObject.SetActive(!hasFailed);
			songArtist.color = hasFailed ? FAIL : PASS;
            headerBorder.color = hasFailed ? FAIL_TRANSLUCENT : PASS_TRANSLUCENT;
		}
    }
}
