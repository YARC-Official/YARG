using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.PlayMode;

namespace YARG.UI {
	public class GameUI : MonoBehaviour {
		[SerializeField]
		private Transform trackContainer;
		[SerializeField]
		private Image songProgress;
		[SerializeField]
		private TextMeshProUGUI songTitle;
		[SerializeField]
		private TextMeshProUGUI lyric;

		public static GameUI Instance {
			get;
			private set;
		} = null;

		private void Awake() {
			Instance = this;
		}

		private void Start() {
			songTitle.text = $"{Play.song.SongName} - {Play.song.artistName}";
		}

		private void Update() {
			songProgress.fillAmount = Play.Instance.SongTime / Play.song.songLength.Value;
		}

		public void AddTrackImage(RenderTexture rt) {
			var trackImage = new GameObject();
			trackImage.transform.parent = trackContainer;
			trackImage.transform.localScale = Vector3.one;

			var rawImage = trackImage.AddComponent<RawImage>();
			rawImage.texture = rt;

			UpdateRawImageSizing();
		}

		public void SetGenericLyric(string str) {
			lyric.text = str;
		}

		private void UpdateRawImageSizing() {
			foreach (var rawImage in trackContainer.GetComponentsInChildren<RawImage>()) {
				float percent = 1f / trackContainer.childCount;
				rawImage.uvRect = new Rect((1f - percent) / 2f, 0f, percent, 1f);
			}
		}
	}
}