using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.PlayMode;
using YARG.Util;

namespace YARG.UI {
	public class GameUI : MonoBehaviour {
		[SerializeField]
		private Transform trackContainer;
		[SerializeField]
		private Image songProgress;
		[SerializeField]
		private TextMeshProUGUI songTitle;
		[SerializeField]
		private TextMeshProUGUI bandName;
		[SerializeField]
		private TextMeshProUGUI lyric;
		[SerializeField]
		private RawImage vocalTrack;
		[SerializeField]
		private TextMeshProUGUI loadingText;

		public GameObject loadingContainer;
		public GameObject pauseMenu;

		public static GameUI Instance {
			get;
			private set;
		} = null;

		private void Awake() {
			Instance = this;
		}

		private void Start() {
			if (Play.speed == 1f) {
				songTitle.text = $"{Play.song.Name}";
				bandName.text = $"{Play.song.Artist}";
			} else {
				songTitle.text = $"{Play.song.Name} ({Play.speed * 100}%)";
				bandName.text = $"{Play.song.Artist}";
			}
		}

		private void Update() {
			songProgress.fillAmount = Play.Instance.SongTime / Play.Instance.SongLength;
		}

		public void AddTrackImage(RenderTexture rt) {
			var trackImage = new GameObject();
			trackImage.transform.parent = trackContainer;
			trackImage.transform.localScale = Vector3.one;

			var rawImage = trackImage.AddComponent<RawImage>();
			rawImage.texture = rt;

			UpdateRawImageSizing();
		}

		public void SetVocalTrackImage(RenderTexture rt) {
			vocalTrack.texture = rt;

			// TODO: Whyyy. figure out a better way to scale.
			var rect = vocalTrack.rectTransform.ToViewportSpace();
			vocalTrack.uvRect = new(0f, rect.y / 1.9f, rect.width, rect.height);
		}

		public void RemoveVocalTrackImage() {
			Destroy(vocalTrack.gameObject);
		}

		public void SetGenericLyric(string str) {
			lyric.text = str;
		}

		private void UpdateRawImageSizing() {
			// Calculate the percent
			float percent = 1f / trackContainer.childCount;
			float heightAdd = 0f;
			for (int i = 2; i < trackContainer.childCount; i++) {
				percent += 0.12f;
				heightAdd += 0.12f * (16f / 9f);
			}

			// Apply UVs
			foreach (var rawImage in trackContainer.GetComponentsInChildren<RawImage>()) {
				rawImage.uvRect = new Rect((1f - percent) / 2f, 0f, percent, 1f + heightAdd);
			}
		}

		public void SetLoadingText(string str) {
			loadingText.text = str;
		}
	}
}