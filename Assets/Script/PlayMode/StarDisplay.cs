using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using YARG.Chart;

namespace YARG.PlayMode {
	public class StarDisplay : MonoBehaviour {
		[SerializeField]
		private List<GameObject> objStars;
		[SerializeField]
		private GameObject objGoldMeterMaster;
		[SerializeField]
		private List<Image> objGoldMeters;
		[SerializeField]
		private RawImage goldMeterLine;

		private int curStar = 0;
		private bool goldAchieved = false;

		// for gold progress meter pulsing
		private int curBar = 0;
		private List<float> bars = new();

		// for positioning the line in gold progress meter
		private float height;

		private void OnEnable() {
			ScoreKeeper.OnScoreChange += OnScoreChange;
		}

		private void OnDisable() {
			ScoreKeeper.OnScoreChange -= OnScoreChange;
		}

		private void Start() {
			var beats = Play.Instance.chart.beats;
			foreach (var ev in beats) {
				if (ev.Style == BeatStyle.MEASURE) {
					bars.Add(ev.Time);
				}
			}

			height = GetComponent<RectTransform>().rect.height;
		}

		private void OnScoreChange() {
			// don't do anything if gold stars have been achieved
			if (!goldAchieved)
				SetStars(StarScoreKeeper.BandStars);
		}

		private void SetStarProgress(GameObject star, double progress) {
			if (!star.activeInHierarchy) {
				star.SetActive(true);
				star.GetComponent<Animator>().Play("PopNew");
			}

			if (progress < 1.0) {
				// update star progress
				star.transform.GetChild(0).GetComponent<Image>().fillAmount = (float) progress;
			} else {
				// fulfill star
				star.transform.GetChild(0).GetComponent<Image>().fillAmount = 1f;
				star.GetComponent<Animator>().Play("TransToComplete");
			}
		}

		/// <summary>
		/// Set the stars to display. Decimal numbers will show progress for the next star. Input >= 6 shows 5 gold stars.
		/// </summary>
		/// <param name="stars"></param>
		public void SetStars(double stars) {
			int topStar = (int) stars;

			double curProgress = stars - (int) stars;

			if (curStar <= 4) {
				// set curStar to next if applicable
				if (topStar > curStar) {
					// next star
					for (int i = curStar; i < topStar; ++i) {
						SetStarProgress(objStars[i], 1.0);
					}
					curStar = topStar;

					GameManager.AudioManager.PlaySoundEffect(SfxSample.StarGain);
				}

				if (curStar <= 4)
					SetStarProgress(objStars[curStar], curProgress);
			}

			// gold star handling
			if (5.0 <= stars && stars < 6.0) {
				// update gold star progress
				foreach (var s in objGoldMeters) {
					s.GetComponent<Image>().fillAmount = (float) curProgress;
				}
				goldMeterLine.rectTransform.anchoredPosition = new Vector2(0, (float) curProgress * height);
			} else if (stars >= 6.0) {
				// show the gold!
				foreach (var s in objStars) {
					s.GetComponent<Animator>().Play("TransToGold");
				}
				// disable progress meters
				objGoldMeterMaster.SetActive(false);

				GameManager.AudioManager.PlaySoundEffect(SfxSample.StarGold);
				goldAchieved = true; // so we stop trying to update
			}
		}

		private void Update() {
			if (goldAchieved) { return; }

			int nextBar = curBar;
			while (nextBar < bars.Count - 1 && bars[nextBar] <= Play.Instance.SongTime) {
				nextBar++;
			}

			if (nextBar > curBar) {
				// animation time
				var time = bars[nextBar] - bars[nextBar - 1];

				// pulse the meter
				var anim = objGoldMeterMaster.GetComponent<Animator>();
				anim.speed = 1 / time;
				anim.Play("GoldMeter", -1, 0);

				curBar = nextBar;
			}
		}
	}
}
