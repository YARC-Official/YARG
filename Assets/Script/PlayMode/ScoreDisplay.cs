using System.IO;
using System.Threading;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.PlayMode {
	public class ScoreDisplay : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI scoreText;

		[Space]
		[SerializeField]
		private RectTransform progressMask;
		[SerializeField]
		private RectTransform progressImg;

		private void Start() {
			scoreText.text = $"<mspace=0.59em>0";
			StartCoroutine(LateStart());
		}

		private IEnumerator LateStart() {
			// wait for MicPlayer.Start to run
			yield return new WaitForEndOfFrame();
			
			// Move score display if MicPlayer is not present
			if (!MicPlayer.Instance) {
				var rt = this.GetComponent<RectTransform>();
				var pos = rt.anchoredPosition;
				pos.y = -93;
				rt.anchoredPosition = pos;
			}
		}

		private void OnEnable() {
			ScoreKeeper.OnScoreChange += OnScoreChange;
		}

		private void OnDisable() {
			ScoreKeeper.OnScoreChange -= OnScoreChange;
		}

		private void OnScoreChange() {
			scoreText.text = $"<mspace=0.59em>{ScoreKeeper.TotalScore:n0}";
		}

		private void SetSongProgress(float progress) {
			var w = GetComponent<RectTransform>().rect.width;
			var mPos = progressMask.anchoredPosition;
			var iPos = progressImg.anchoredPosition;

			mPos.x = progress * w;
			iPos.x = w - mPos.x;

			progressMask.anchoredPosition = mPos;
			progressImg.anchoredPosition = iPos;
		}

		private void Update() {
			if (!Play.Instance.endReached) {
				var songProgress = Play.Instance.SongTime / Play.Instance.SongLength;
				SetSongProgress(songProgress);
			} else {
				SetSongProgress(1f);
			}
		}
	}
}
