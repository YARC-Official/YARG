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
			header.text = $"{Play.song.SongName} - {Play.song.artistName}";

			// Create a score to push

			var songScore = new SongScore {
				lastPlayed = DateTime.Now,
				timesPlayed = 1,
				highestPercent = new()
			};
			var oldScore = ScoreManager.GetScore(Play.song);

			HashSet<PlayerManager.Player> highScores = new();
			HashSet<PlayerManager.Player> disqualified = new();
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

				// Override or add percentage
				if (oldScore == null ||
					!oldScore.highestPercent.TryGetValue(player.chosenInstrument, out var oldHighest) ||
					lastScore.percentage > oldHighest) {

					songScore.highestPercent[player.chosenInstrument] = lastScore.percentage;
					highScores.Add(player);
				}
			}

			// Push!
			ScoreManager.PushScore(Play.song, songScore);

			// Show score sections
			foreach (var player in PlayerManager.players) {
				// Bind input events
				player.inputStrategy.GenericNavigationEvent += OnGenericNavigation;

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
		}

		private void OnDisable() {
			// Unbind input events
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericNavigationEvent -= OnGenericNavigation;
			}
		}

		private void Update() {
			GameManager.client?.CheckForSignals();

			// Enter
			if (Keyboard.current.enterKey.wasPressedThisFrame) {
				MainMenu.Instance.ShowMainMenu();
			}

			// Update player navigation
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.UpdateNavigationMode();
			}
		}

		private void OnGenericNavigation(NavigationType navigationType, bool firstPressed) {
			if (!firstPressed) {
				return;
			}

			if (navigationType == NavigationType.PRIMARY) {
				MainMenu.Instance.ShowMainMenu();
			}
		}
	}
}