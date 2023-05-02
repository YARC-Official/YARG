using System.Collections.Generic;
using UnityEngine;
using YARG.Data;

namespace YARG.UI.MusicLibrary {
	public class Sidebar : MonoBehaviour {
		[SerializeField]
		private Transform difficultyRingsContainer;

		[Space]
		[SerializeField]
		private GameObject difficultyRingPrefab;

		private List<DifficultyRing> difficultyRings = new();

		public void Init() {
			// Spawn 10 difficulty rings
			for (int i = 0; i < 10; i++) {
				var go = Instantiate(difficultyRingPrefab, difficultyRingsContainer);
				difficultyRings.Add(go.GetComponent<DifficultyRing>());
			}
		}

		public void UpdateSidebar() {
			var songInfo = SongSelection.Instance.SelectedSong;

			/*
			
				Guitar               ; Bass               ; 4 or 5 lane ; Keys     ; Mic (dependent on mic count) 
				Pro Guitar or Co-op  ; Pro Bass or Rhythm ; True Drums  ; Pro Keys ; Band
			
			*/

			difficultyRings[0].SetInfo(songInfo.partDifficulties, Instrument.GUITAR);
			difficultyRings[1].SetInfo(songInfo.partDifficulties, Instrument.BASS);

			// 5-lane or 4-lane
			if (songInfo.drumType == SongInfo.DrumType.FIVE_LANE) {
				difficultyRings[2].SetInfo(songInfo.partDifficulties, Instrument.GH_DRUMS);
			} else {
				difficultyRings[2].SetInfo(songInfo.partDifficulties, Instrument.DRUMS);
			}

			difficultyRings[3].SetInfo(songInfo.partDifficulties, Instrument.KEYS);

			// Mic (with mic count)
			if (songInfo.partDifficulties[Instrument.HARMONY] == -1) {
				difficultyRings[4].SetInfo(songInfo.partDifficulties, Instrument.VOCALS);
			} else {
				difficultyRings[4].SetInfo(songInfo.partDifficulties, Instrument.HARMONY);
			}

			// Protar or Co-op
			if (songInfo.drumType == SongInfo.DrumType.FIVE_LANE &&
				songInfo.partDifficulties[Instrument.REAL_GUITAR] == -1) {

				difficultyRings[5].SetInfo(songInfo.partDifficulties, Instrument.GUITAR_COOP);
			} else {
				difficultyRings[5].SetInfo(songInfo.partDifficulties, Instrument.REAL_GUITAR);
			}

			// Pro bass or Rhythm
			if (songInfo.drumType == SongInfo.DrumType.FIVE_LANE &&
				songInfo.partDifficulties[Instrument.REAL_BASS] == -1) {

				difficultyRings[6].SetInfo(songInfo.partDifficulties, Instrument.RHYTHM);
			} else {
				difficultyRings[6].SetInfo(songInfo.partDifficulties, Instrument.REAL_BASS);
			}

			difficultyRings[7].SetInfo(Instrument.REAL_DRUMS, -1);
			difficultyRings[8].SetInfo(songInfo.partDifficulties, Instrument.REAL_KEYS);
			difficultyRings[9].SetInfo(Instrument.REAL_DRUMS, -1);
		}
	}
}