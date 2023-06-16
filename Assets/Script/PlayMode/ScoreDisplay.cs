using System.IO;
using System.Threading;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using YARG.UI;
namespace YARG.PlayMode {
	public class ScoreDisplay : MonoBehaviour {
		[SerializeField]
		private TextMeshProUGUI scoreText;

		[Space]
		[SerializeField]
		private ProgressBarFadedEdge songProgress;

		private void Start() {
			scoreText.text = $"<mspace=0.538em>0";
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
			scoreText.text = $"<mspace=0.538em>{ScoreKeeper.TotalScore:n0}";
		}

		private void Update() {
			if (!Play.Instance.endReached) {
				songProgress.SetProgress(Play.Instance.SongTime / Play.Instance.SongLength);
			} else {
				songProgress.SetProgress(1f);
			}
		}
	}
}
