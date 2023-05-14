using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.PlayMode;

namespace YARG.UI {
	public class TrackView : MonoBehaviour {
		[field: SerializeField]
		public RawImage TrackImage { get; private set; }

		[Space]
		[SerializeField]
		private TextMeshProUGUI _performanceText;
		[SerializeField]
		private PerformanceTextSizer _performanceTextSizer;

		[Space]
		[SerializeField]
		private TextMeshProUGUI _soloTopText;
		[SerializeField]
		private TextMeshProUGUI _soloBottomText;
		[SerializeField]
		private TextMeshProUGUI _soloFullText;
		[SerializeField]
		private CanvasGroup _soloBoxCanvasGroup;
		[SerializeField]
		private Image _soloBox;

		[Space]
		[SerializeField]
		private Sprite _normalSoloBox;

		private bool _soloBoxShowing = false;

		private void Start() {
			_performanceTextSizer = new(24f, 3f);
		}

		public void UpdateSizing(int trackCount) {
			float scale = Mathf.Max(0.7f * Mathf.Log10(trackCount - 1), 0f);
			scale = 1f - scale;

			TrackImage.transform.localScale = new Vector3(scale, scale, scale);
		}

		public void SetSoloBox(string topText, string bottomText) {
			// Show if hidden
			if (!_soloBoxShowing) {
				// Stop hide coroutine, if we are already hiding
				StopCoroutine("HideSoloBoxCoroutine");

				_soloBox.gameObject.SetActive(true);
				_soloBoxCanvasGroup.alpha = 1f;

				_soloBox.sprite = _normalSoloBox;
				_soloBoxShowing = false;

				_soloFullText.text = string.Empty;
			}

			_soloTopText.text = topText;
			_soloBottomText.text = bottomText;
		}

		public void HideSoloBox(string percent, string fullText) {
			_soloBoxShowing = false;

			_soloTopText.text = string.Empty;
			_soloBottomText.text = string.Empty;
			_soloFullText.text = percent;

			StartCoroutine(HideSoloBoxCoroutine(fullText));
		}

		private IEnumerator HideSoloBoxCoroutine(string fullText) {
			yield return new WaitForSeconds(1f);

			_soloFullText.text = fullText;

			yield return new WaitForSeconds(1f);

			yield return _soloBoxCanvasGroup
				.DOFade(0f, 0.25f)
				.WaitForCompletion();

			_soloBox.gameObject.SetActive(false);
		}

		public void ShowPerformanceText(string text) {
			StopCoroutine("SizePerformanceText");
			StartCoroutine(SizePerformanceText(text));
		}

		private IEnumerator SizePerformanceText(string text) {
			_performanceText.text = text;
			_performanceTextSizer.ResetAnimationTime();

			while (_performanceTextSizer.AnimTimeRemaining > 0f) {
				_performanceTextSizer.AnimTimeRemaining -= Time.deltaTime;
				_performanceText.fontSize = _performanceTextSizer.PerformanceTextFontSize();

				// Update animation every frame
				yield return null;
			}

			_performanceText.text = string.Empty;
		}
	}
}