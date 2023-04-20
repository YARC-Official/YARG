using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

namespace YARG.PlayMode {
    public class StarDisplay : MonoBehaviour {
		[SerializeField]
		private List<GameObject> objStars;
		[SerializeField]
		private List<GameObject> objGoldMeters;

		private int curStar = 0;
		private bool goldAchieved = false;

		private void OnEnable() {
			ScoreKeeper.OnScoreChange += OnScoreChange;
		}

        private void OnDisable() {
            ScoreKeeper.OnScoreChange -= OnScoreChange;
        }

        private void OnScoreChange() {
			SetStars(StarScoreKeeper.BandStars);
		}

        private void SetStarProgress(GameObject star, double progress) {
			if (!star.activeInHierarchy) {
                star.SetActive(true);
                star.GetComponent<Animator>().Play("PopNew");
            }

			if (progress < 1.0) {
				// update star progress
				star.transform.GetChild(0).GetComponent<Image>().fillAmount = (float)progress;
			}
            else {
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
			if (goldAchieved) { return; }

			int topStar = (int)stars;

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
					s.GetComponent<Image>().fillAmount = (float)curProgress;
				}
            }
			else if (stars >= 6.0) {
                // show the gold!
                foreach (var s in objStars) {
                    s.GetComponent<Animator>().Play("TransToGold");
				}
                // disable progress meters
                foreach (var s in objGoldMeters) {
					s.SetActive(false);
				}

                GameManager.AudioManager.PlaySoundEffect(SfxSample.StarGold);

				goldAchieved = true; // so we stop trying to update
			}
		}
    }
}
