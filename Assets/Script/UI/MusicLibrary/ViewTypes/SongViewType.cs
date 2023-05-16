using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.UI.MusicLibrary.ViewTypes {
	public class SongViewType : ViewType {
		public override BackgroundType Background => BackgroundType.Normal;

		public override string PrimaryText => SongEntry.Name;
		public override string SecondaryText => SongEntry.Artist;
		public override bool UseAsMadeFamousBy => !SongEntry.IsMaster;

		public override string SideText {
			get {
				// Song score
				var score = ScoreManager.GetScore(SongEntry);
				if (score == null || score.highestPercent.Count <= 0) {
					return string.Empty;
				} else {
					var (instrument, highest) = score.GetHighestPercent();
					return $"<sprite name=\"{instrument}\"> <b>{highest.difficulty.ToChar()}</b> {Mathf.Floor(highest.percent * 100f):N0}%";
				}
			}
		}

		public override Sprite IconSprite {
			get {
				if (SongEntry.Source == null) {
					return Resources.Load<Sprite>("Sources/custom");
				}

				string folderPath = $"Sources/{SongEntry.Source}";
				var loadedSprite = Resources.Load<Sprite>(folderPath);

				if (loadedSprite == null) {
					return Resources.Load<Sprite>("Sources/custom");
				}

				return loadedSprite;
			}
		}

		public SongEntry SongEntry { get; private set; }

		public SongViewType(SongEntry songEntry) {
			SongEntry = songEntry;
		}

		public override void SecondaryTextClick() {
			base.SecondaryTextClick();

			SongSelection.Instance.searchField.text = $"artist:{SongEntry.Artist}";
		}

		public override void PrimaryButtonClick() {
			base.PrimaryButtonClick();

			MainMenu.Instance.ShowPreSong();
		}

		public override void IconClick() {
			base.IconClick();

			SongSelection.Instance.searchField.text = $"source:{SongEntry.Source}";
		}
	}
}