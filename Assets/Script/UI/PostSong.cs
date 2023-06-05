using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI {
	public class PostSong : MonoBehaviour {
		[SerializeField]
		private GameObject scoreSection;

		[SerializeField]
		private TextMeshProUGUI header;
		[SerializeField]
		private Transform scoreContainer;

		private void OnEnable() {
			if (Play.speed == 1f) {
				header.text = $"{GameManager.Instance.SelectedSong.Name} - {GameManager.Instance.SelectedSong.Artist}";
			} else {
				header.text = $"{GameManager.Instance.SelectedSong.Name} ({Play.speed * 100}% speed) - {GameManager.Instance.SelectedSong.Artist}";
			}

			// Create a score to push

			var songScore = new SongScore {
				lastPlayed = DateTime.Now,
				timesPlayed = 1,
				highestPercent = new(),
				highestScore = new()
			};
			var oldScore = ScoreManager.GetScore(GameManager.Instance.SelectedSong);

			HashSet<PlayerManager.Player> highScores = new();
			HashSet<PlayerManager.Player> disqualified = new();
			foreach (var player in PlayerManager.players) {
				// Skip "Sit Out"s
				if (player.chosenInstrument == null) {
					continue;
				}

				// DQ lower than 100% speeds
				if (Play.speed < 1f) {
					disqualified.Add(player);
					continue;
				}

				// DQ bots
				if (player.inputStrategy.BotMode) {
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
				if (oldScore == null || oldScore.highestScore == null ||
					!oldScore.highestScore.TryGetValue(player.chosenInstrument, out var oldHighestSc) ||
					lastScore.score > oldHighestSc) {

					songScore.highestPercent[player.chosenInstrument] = lastScore.percentage;
					songScore.highestScore[player.chosenInstrument] = lastScore.score;
					highScores.Add(player);
				}
			}

			// Push!
			ScoreManager.PushScore(GameManager.Instance.SelectedSong, songScore);

			// Show score sections
			foreach (var player in PlayerManager.players) {
				// Get score type
				var type = ScoreSection.ScoreType.NORMAL;
				if (disqualified.Contains(player)) {
					type = ScoreSection.ScoreType.DISQUALIFIED;
				} else if (highScores.Contains(player)) {
					type = ScoreSection.ScoreType.HIGH_SCORE;
				}

				// Add scores
				var score = Instantiate(scoreSection, scoreContainer).GetComponent<ScoreSection>();
				score.SetScore(player, type);
			}

			// Bind input events
			Navigator.Instance.NavigationEvent += NavigationEvent;
		}

		private void OnDisable() {
			// Unbind input events
			Navigator.Instance.NavigationEvent -= NavigationEvent;
		}

		private void NavigationEvent(NavigationContext ctx) {
			if (ctx.Action == MenuAction.Confirm) {
				MainMenu.Instance.ShowSongSelect();
			}
		}
	}
}