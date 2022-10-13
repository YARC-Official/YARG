using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class Game : MonoBehaviour {
	public const float HIT_MARGIN = 0.075f;

	[SerializeField]
	private GameObject soundAudioPrefab;
	[SerializeField]
	private GameObject trackPrefab;

	public delegate void FretPressAction(bool on, int fret);
	public event FretPressAction FretPressEvent;

	public static Game Instance {
		get;
		private set;
	} = null;

	private YargInput input;
	private bool songStarted = false;

	private float realSongTime = 0f;
	private float calibration = 0f;
	public float SongTime {
		get => realSongTime + calibration;
	}

	public float SongSpeed {
		get;
		private set;
	}
	public List<NoteInfo> Chart {
		get;
		private set;
	}

	public bool StrumThisFrame {
		get;
		private set;
	} = false;

	private int botChartIndex = 0;
	public bool BotMode {
		get;
		private set;
	} = false;

	private void Awake() {
		Instance = this;

		Chart = new();
		realSongTime = -0.5f;
		calibration = -0.11f;
		SongSpeed = 5f;

		// Input

		input = new YargInput();
		input.Enable();

		input._5Fret.Green.started += _ => FretPress(0);
		input._5Fret.Red.started += _ => FretPress(1);
		input._5Fret.Yellow.started += _ => FretPress(2);
		input._5Fret.Blue.started += _ => FretPress(3);
		input._5Fret.Orange.started += _ => FretPress(4);
		input._5Fret.Strum.started += _ => Strum(true);

		input._5Fret.Green.canceled += _ => FretRelease(0);
		input._5Fret.Red.canceled += _ => FretRelease(1);
		input._5Fret.Yellow.canceled += _ => FretRelease(2);
		input._5Fret.Blue.canceled += _ => FretRelease(3);
		input._5Fret.Orange.canceled += _ => FretRelease(4);
		input._5Fret.Strum.canceled += _ => Strum(false);

		// Song

		var songFolder = new DirectoryInfo("B:\\Clone Hero Alpha\\Songs\\Phish - Llama");
		StartCoroutine(StartSong(songFolder));
	}

	private IEnumerator StartSong(DirectoryInfo songFolder) {
		// Load audio
		List<AudioSource> audioSources = new();
		foreach (var file in songFolder.GetFiles("*.ogg")) {
			// Load file
			using UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(file.FullName, AudioType.OGGVORBIS);
			yield return uwr.SendWebRequest();
			var clip = DownloadHandlerAudioClip.GetContent(uwr);

			// Create audio source
			var songAudio = Instantiate(soundAudioPrefab, transform);
			var audioSource = songAudio.GetComponent<AudioSource>();
			audioSource.clip = clip;
			audioSources.Add(audioSource);
		}

		// Load midi
		Parser.Parse(Path.Combine(songFolder.FullName, "notes.mid"), Chart);

		// Spawn track
		Instantiate(trackPrefab);

		songStarted = true;

		// Start all audio at the same time
		foreach (var audioSource in audioSources) {
			audioSource.Play();
		}
	}

	private void Update() {
		if (songStarted) {
			realSongTime += Time.deltaTime;

			if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
				calibration += 0.01f;
				Debug.Log(calibration);
			}

			if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
				calibration -= 0.01f;
				Debug.Log(calibration);
			}

			if (Keyboard.current.lKey.wasPressedThisFrame) {
				long i;
				for (i = 0; i < 2_000_000_000; i++) { }
				Debug.Log(i);
			}
		} else {
			if (Keyboard.current.bKey.wasPressedThisFrame) {
				BotMode = !BotMode;
				Debug.Log(BotMode);
			}
		}

		// Update bot mode
		if (BotMode) {
			while (Chart.Count > botChartIndex && Chart[botChartIndex].time <= Instance.SongTime) {
				var noteInfo = Chart[botChartIndex];

				FretPress(noteInfo.fret);
				StrumThisFrame = true;
				botChartIndex++;
			}
		}
	}

	private void LateUpdate() {
		StrumThisFrame = false;

		// Update bot mode
		if (BotMode) {
			for (int i = 0; i < 5; i++) {
				FretRelease(i);
			}
		}
	}

	private void FretPress(int fret) {
		FretPressEvent?.Invoke(true, fret);
	}

	private void FretRelease(int fret) {
		FretPressEvent?.Invoke(false, fret);
	}

	private void Strum(bool on) {
		StrumThisFrame = on;
	}
}