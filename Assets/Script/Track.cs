using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Input;
using YARG.Pools;
using YARG.UI;
using YARG.Utils;

namespace YARG {
	public class Track : MonoBehaviour {
		public const float TRACK_SPAWN_OFFSET = 3f;

		public PlayerManager.Player player;

		private bool strummed = false;
		private FiveFretInputStrategy input;

		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private MeshRenderer trackRenderer;
		[SerializeField]
		private Transform hitWindow;

		[Space]
		[SerializeField]
		private Fret[] frets;
		[SerializeField]
		private Color[] fretColors;
		[SerializeField]
		private NotePool notePool;
		[SerializeField]
		private Pool genericPool;

		[Space]
		[SerializeField]
		private TextMeshPro comboText;
		[SerializeField]
		private MeshRenderer comboMeterRenderer;

		private int visualChartIndex = 0;
		private int realChartIndex = 0;
		private int eventChartIndex = 0;

		private Queue<List<NoteInfo>> expectedHits = new();
		private List<NoteInfo> heldNotes = new();

		private int _combo = 0;
		private int Combo {
			get => _combo;
			set => _combo = value;
		}
		private int Multiplier => Mathf.Min(Combo / 10 + 1, 4);

		private void Awake() {
			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.DefaultHDR
			);
			descriptor.mipCount = 0;
			var renderTexture = new RenderTexture(descriptor);
			trackCamera.targetTexture = renderTexture;

			// Set up camera
			var info = trackCamera.GetComponent<UniversalAdditionalCameraData>();
			if (PlayerManager.LowQualityMode) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}
		}

		private void Start() {
			notePool.player = player;
			genericPool.player = player;

			// Inputs

			input = (FiveFretInputStrategy) player.inputStrategy;
			input.ResetForSong();

			input.FretChangeEvent += FretChangedAction;
			input.StrumEvent += StrumAction;

			// Set render texture
			GameUI.Instance.AddTrackImage(trackCamera.targetTexture);

			// Spawn in frets
			for (int i = 0; i < 5; i++) {
				var fret = frets[i].GetComponent<Fret>();
				fret.SetColor(fretColors[i]);
				frets[i] = fret;
			}

			// Adjust hit window
			var scale = hitWindow.localScale;
			hitWindow.localScale = new(scale.x, Game.HIT_MARGIN * player.trackSpeed * 2f, scale.z);
		}

		private void OnDestroy() {
			// Release render texture
			trackCamera.targetTexture.Release();

			// Unbind input
			input.FretChangeEvent -= FretChangedAction;
			input.StrumEvent -= StrumAction;
		}

		private void Update() {
			// Get chart stuff
			var chart = Game.Instance.chart
				.GetChartByName(player.chosenInstrument)[player.chosenDifficulty];
			var events = Game.Instance.chart.events;

			// Update input strategy
			if (input.botMode) {
				input.UpdateBotMode(chart, Game.Instance.SongTime);
			} else {
				input.UpdatePlayerMode();
			}

			// Update track UV
			var trackMaterial = trackRenderer.material;
			var oldOffset = trackMaterial.GetVector("TexOffset");
			float movement = Time.deltaTime * player.trackSpeed / 4f;
			trackMaterial.SetVector("TexOffset", new(oldOffset.x, oldOffset.y - movement));

			// Ignore everything else until the song starts
			if (!Game.Instance.SongStarted) {
				return;
			}

			// Update groove mode
			float currentGroove = trackMaterial.GetFloat("GrooveState");
			if (Multiplier == 4) {
				trackMaterial.SetFloat("GrooveState", Mathf.Lerp(currentGroove, 1f, Time.deltaTime * 5f));
			} else {
				trackMaterial.SetFloat("GrooveState", Mathf.Lerp(currentGroove, 0f, Time.deltaTime * 3f));
			}

			float relativeTime = Game.Instance.SongTime + ((TRACK_SPAWN_OFFSET + 1.75f) / player.trackSpeed);

			// Since chart is sorted, this is guaranteed to work
			while (chart.Count > visualChartIndex && chart[visualChartIndex].time <= relativeTime) {
				var noteInfo = chart[visualChartIndex];

				SpawnNote(noteInfo, relativeTime);
				visualChartIndex++;
			}

			// Update events
			while (events.Count > eventChartIndex && events[eventChartIndex].time <= relativeTime) {
				var eventInfo = events[eventChartIndex];

				float compensation = TRACK_SPAWN_OFFSET - CalcLagCompensation(relativeTime, eventInfo.time);
				if (eventInfo.name == "beatLine_minor") {
					genericPool.Add("beatLine_minor", new(0f, 0.01f, compensation));
				} else if (eventInfo.name == "beatLine_major") {
					genericPool.Add("beatLine_major", new(0f, 0.01f, compensation));
				}

				eventChartIndex++;
			}

			// Update expected input
			while (chart.Count > realChartIndex && chart[realChartIndex].time <= Game.Instance.SongTime + Game.HIT_MARGIN) {
				var noteInfo = chart[realChartIndex];

				var peeked = expectedHits.ReversePeekOrNull();
				if (peeked?[0].time == noteInfo.time) {
					// Add notes as chords
					peeked.Add(noteInfo);
				} else {
					// Or add notes as singular
					var l = new List<NoteInfo>(5) { noteInfo };
					expectedHits.Enqueue(l);
				}

				realChartIndex++;
			}

			// Update real input
			UpdateInput();

			// Update held notes
			for (int i = heldNotes.Count - 1; i >= 0; i--) {
				var heldNote = heldNotes[i];
				if (heldNote.time + heldNote.length <= Game.Instance.SongTime) {
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}

			// Update info (combo, multiplier, etc.)
			UpdateInfo();

			// Un-strum
			strummed = false;
		}

		private void UpdateInfo() {
			// Update text
			if (Multiplier == 1) {
				comboText.text = null;
			} else {
				comboText.text = $"{Multiplier}<sub>x</sub>";
			}

			// Update status

			int index = Combo % 10;
			if (Multiplier != 1 && index == 0) {
				index = 10;
			} else if (Multiplier == 4) {
				index = 10;
			}

			comboMeterRenderer.material.SetFloat("SpriteNum", index);
		}

		private void UpdateInput() {
			// Handle misses (multiple a frame in case of lag)
			while (Game.Instance.SongTime - expectedHits.PeekOrNull()?[0].time > Game.HIT_MARGIN) {
				var missedChord = expectedHits.Dequeue();

				// Call miss for each component
				Combo = 0;
				foreach (var hit in missedChord) {
					notePool.MissNote(hit);
				}
			}

			if (expectedHits.Count <= 0) {
				// Handle ghost inputs
				if (strummed) {
					Combo = 0;

					// Let go of held notes
					for (int i = heldNotes.Count - 1; i >= 0; i--) {
						var heldNote = heldNotes[i];
						notePool.MissNote(heldNote);
						heldNotes.RemoveAt(i);
						frets[heldNote.fret].StopSustainParticles();
					}
				}

				return;
			}

			// Handle hits (one per frame so no double hits)
			var chord = expectedHits.Peek();
			if (!chord[0].hopo && !strummed) {
				return;
			} else if (chord[0].hopo && Combo <= 0 && !strummed) {
				return;
			}

			// Convert NoteInfo list to chord fret array
			int[] chordInts = new int[chord.Count];
			for (int i = 0; i < chordInts.Length; i++) {
				chordInts[i] = chord[i].fret;
			}

			// Check if correct chord is pressed
			if (!ChordPressed(chordInts)) {
				if (!chord[0].hopo) {
					Combo = 0;
				}

				return;
			}

			// If so, hit!
			expectedHits.Dequeue();

			Combo++;
			foreach (var hit in chord) {
				// Hit notes
				notePool.HitNote(hit);

				// Play particles
				frets[hit.fret].PlayParticles();

				// If sustained, add to held
				if (hit.length > 0.2f) {
					heldNotes.Add(hit);
					frets[hit.fret].PlaySustainParticles();
				}
			}
		}

		private bool ChordPressed(int[] chord) {
			if (chord.Length == 1) {
				// Deal with single notes
				int fret = chord[0];
				for (int i = 0; i < frets.Length; i++) {
					if (frets[i].IsPressed && i > fret) {
						return false;
					} else if (!frets[i].IsPressed && i == fret) {
						return false;
					} else if (frets[i].IsPressed && i != fret && !Game.ANCHORING) {
						return false;
					}
				}
			} else {
				// Deal with multi-key chords
				for (int i = 0; i < frets.Length; i++) {
					bool contains = chord.Contains(i);
					if (contains && !frets[i].IsPressed) {
						return false;
					} else if (!contains && frets[i].IsPressed) {
						return false;
					}
				}
			}

			return true;
		}

		private void FretChangedAction(bool pressed, int fret) {
			frets[fret].SetPressed(pressed);

			if (!pressed) {
				for (int i = heldNotes.Count - 1; i >= 0; i--) {
					var heldNote = heldNotes[i];
					if (heldNote.fret != fret) {
						continue;
					}

					notePool.MissNote(heldNote);
					heldNotes.RemoveAt(i);
					frets[heldNote.fret].StopSustainParticles();
				}
			}
		}

		private void StrumAction() {
			strummed = true;
		}

		private void SpawnNote(NoteInfo noteInfo, float time) {
			// Set correct position
			float lagCompensation = CalcLagCompensation(time, noteInfo.time);
			float x = frets[noteInfo.fret].transform.localPosition.x;
			var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

			// Set note info
			var noteComp = notePool.CreateNote(noteInfo, pos);
			noteComp.SetInfo(fretColors[noteInfo.fret], noteInfo.length, noteInfo.hopo);
		}

		private float CalcLagCompensation(float currentTime, float noteTime) {
			return (currentTime - noteTime) * player.trackSpeed;
		}
	}
}