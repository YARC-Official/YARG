using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.UI {
	public class TrackView : MonoBehaviour {
		[field: SerializeField]
		public RawImage TrackImage { get; private set; }
		[SerializeField]
		private AspectRatioFitter _aspectRatioFitter;

		[Space]
		[SerializeField]
		private TextMeshProUGUI _performanceText;
		[SerializeField]
		private PerformanceTextScaler _performanceTextScaler;

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

		private Coroutine _soloBoxHide = null;

		private void Start() {
			_performanceTextScaler = new(3f);
			_performanceText.text = "";
			_aspectRatioFitter.aspectRatio = (float) Screen.width / Screen.height;
		}

		public void UpdateSizing(int trackCount) {
			float scale = Mathf.Max(0.7f * Mathf.Log10(trackCount - 1), 0f);
			scale = 1f - scale;

			TrackImage.transform.localScale = new Vector3(scale, scale, scale);
		}

		public void SetSoloBox(string topText, string bottomText) {
			// Stop hide coroutine if we were previously hiding
			if (_soloBoxHide != null) {
				StopCoroutine(_soloBoxHide);
				_soloBoxHide = null;
			}

			_soloBox.gameObject.SetActive(true);
			_soloBoxCanvasGroup.alpha = 1f;
			_soloBox.sprite = _normalSoloBox;

			_soloFullText.text = string.Empty;
			_soloTopText.text = topText;
			_soloBottomText.text = bottomText;
		}

		public void HideSoloBox(string percent, string fullText) {
			_soloTopText.text = string.Empty;
			_soloBottomText.text = string.Empty;
			_soloFullText.text = percent;

			_soloBoxHide = StartCoroutine(HideSoloBoxCoroutine(fullText));
		}

		private IEnumerator HideSoloBoxCoroutine(string fullText) {
			yield return new WaitForSeconds(1f);

			_soloFullText.text = fullText;

			yield return new WaitForSeconds(1f);

			yield return _soloBoxCanvasGroup
				.DOFade(0f, 0.25f)
				.WaitForCompletion();

			_soloBox.gameObject.SetActive(false);
			_soloBoxHide = null;
		}

		public void ShowPerformanceText(string text) {
			if (SettingsManager.Settings.DisableTextNotifications.Data) {
				return;
			}

			StopCoroutine(nameof(ScalePerformanceText));
			StartCoroutine(ScalePerformanceText(text));
		}

		private IEnumerator ScalePerformanceText(string text) {
			var rect = _performanceText.rectTransform;
			rect.localScale = Vector3.zero;

			_performanceText.text = text;
			_performanceTextScaler.ResetAnimationTime();

			while (_performanceTextScaler.AnimTimeRemaining > 0f) {
				_performanceTextScaler.AnimTimeRemaining -= Time.deltaTime;
				var scale = _performanceTextScaler.PerformanceTextScale();
				rect.localScale = new Vector3(scale, scale, scale);

				// Update animation every frame
				yield return null;
			}

			_performanceText.text = string.Empty;
		}
	}
}