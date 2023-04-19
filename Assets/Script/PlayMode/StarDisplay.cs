using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

namespace YARG.PlayMode {
    public class StarDisplay : MonoBehaviour {
		[SerializeField]
		private List<GameObject> objStars;
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
				// star is still in progress
				star.transform.GetChild(0).GetComponent<Image>().fillAmount = (float)progress;
			}
            else {
				// fulfill star
                star.transform.GetChild(0).GetComponent<Image>().fillAmount = 1f;
				star.GetComponent<Animator>().Play("TransToComplete");
				// star.transform.GetChild(1).gameObject.SetActive(true);
			}
        }

        /// <summary>
		/// Set the stars to display. Decimal numbers will show progress for the next star. Input >= 6 shows 5 gold stars.
		/// </summary>
		/// <param name="stars"></param>
        public void SetStars(double stars) {
			if (goldAchieved) { return; }

			int topStar = (int)stars;

            if (curStar <= 4) {
                // set curStar to next if applicable
                if (topStar > curStar) {
                    // new star
                    for (int i = curStar; i < topStar; ++i) {
                        SetStarProgress(objStars[i], 1.0);
                    }
                    curStar = topStar;

                    // TODO: sound
                }

                if (curStar <= 4)
				    SetStarProgress(objStars[curStar], stars - (int) stars);
            }

            // gold star handling
            if (5.0 <= stars && stars < 6.0) {
                // TODO: set gold star progress
            }
			else if (stars >= 6.0) {
                // set gold
                foreach (var s in objStars) {
                    s.GetComponent<Animator>().Play("TransToGold");;
                }
                // TODO: sound
				goldAchieved = true; // so we stop trying to update
			}
		}
    }
}
