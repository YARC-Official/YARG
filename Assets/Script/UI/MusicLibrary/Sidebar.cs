using System.Collections.Generic;
using UnityEngine;
using YARG.Data;
using YARG.Song;

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
			var songEntry = SongSelection.Instance.SelectedSong;

			/*
			
				Guitar               ; Bass               ; 4 or 5 lane ; Keys     ; Mic (dependent on mic count) 
				Pro Guitar or Co-op  ; Pro Bass or Rhythm ; True Drums  ; Pro Keys ; Band
			
			*/

			difficultyRings[0].SetInfo(songEntry, Instrument.GUITAR);
			difficultyRings[1].SetInfo(songEntry, Instrument.BASS);

			// 5-lane or 4-lane
			if (songEntry.DrumType == DrumType.FiveLane) {
				difficultyRings[2].SetInfo(songEntry, Instrument.GH_DRUMS);
			} else {
				difficultyRings[2].SetInfo(songEntry, Instrument.DRUMS);
			}

			difficultyRings[3].SetInfo(songEntry, Instrument.KEYS);

			// Mic (with mic count)
			if (songEntry.PartDifficulties.GetValueOrDefault(Instrument.HARMONY, -1) == -1) {
				difficultyRings[4].SetInfo(songEntry, Instrument.VOCALS);
			} else {
				difficultyRings[4].SetInfo(songEntry, Instrument.HARMONY);
			}

			// Protar or Co-op
			int realGuitarDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.REAL_GUITAR, -1);
			if (songEntry.DrumType == DrumType.FourLane && realGuitarDiff == -1) {
				difficultyRings[5].SetInfo(songEntry, Instrument.GUITAR_COOP);
			} else {
				difficultyRings[5].SetInfo(songEntry, Instrument.REAL_GUITAR);
			}

			// Pro bass or Rhythm
			int realBassDiff = songEntry.PartDifficulties.GetValueOrDefault(Instrument.REAL_BASS, -1);
			if (songEntry.DrumType == DrumType.FiveLane && realBassDiff == -1) {
				difficultyRings[6].SetInfo(songEntry, Instrument.RHYTHM);
			} else {
				difficultyRings[6].SetInfo(songEntry, Instrument.REAL_BASS);
			}

			difficultyRings[7].SetInfo(false, "trueDrums", -1);
			difficultyRings[8].SetInfo(songEntry, Instrument.REAL_KEYS);
			difficultyRings[9].SetInfo(false, "band", -1);
		}
	}
}