using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Input {
	public sealed class MicInputStrategy : InputStrategy {
		public const string CONFIRM = "confirm";
		public const string BACK = "back";
		public const string MENU_ACTION_1 = "menu_action_1";
		public const string MENU_ACTION_2 = "menu_action_2";
		public const string MENU_ACTION_3 = "menu_action_3";

		public const string PAUSE = "pause";
		public const string UP = "up";
		public const string DOWN = "down";

		private List<LyricInfo> botChart;

		public float VoicePitch { get; private set; }
		public float VoiceAmplitude { get; private set; }
		public float VoiceNote { get; private set; }
		public int VoiceOctave { get; private set; }

		public bool VoiceDetected { get; private set; }

		public float TimeSinceNoVoice { get; private set; }
		public float TimeSinceVoiceDetected { get; private set; }

		private LyricInfo _botLyricInfo;

		public MicInputStrategy() {
			InputMappings = new() {
				{ CONFIRM,       new(BindingType.BUTTON, "Confirm/Select (Green)", CONFIRM) },
				{ BACK,          new(BindingType.BUTTON, "Back (Red)", BACK) },
				{ MENU_ACTION_1, new(BindingType.BUTTON, "Menu Action 1 (Yellow)", MENU_ACTION_1) },
				{ MENU_ACTION_2, new(BindingType.BUTTON, "Menu Action 2 (Blue)", MENU_ACTION_2) },
				{ MENU_ACTION_3, new(BindingType.BUTTON, "Menu Action 3 (Orange)", MENU_ACTION_3) },

				{ PAUSE,         new(BindingType.BUTTON, "Pause", PAUSE) },
				{ UP,            new(BindingType.BUTTON, "Navigate Up", UP) },
				{ DOWN,          new(BindingType.BUTTON, "Navigate Down", DOWN) },
			};
		}

		public override string GetIconName() {
			return "vocals";
		}

		protected override void UpdatePlayerMode() { }

		protected override void OnUpdate() {
			base.OnUpdate();

			// Skip if bot, or no mic set
			if (BotMode || MicDevice == null) {
				return;
			}

			// Set info from mic
			VoiceDetected = MicDevice.VoiceDetected;
			VoiceAmplitude = MicDevice.Amplitude;
			VoicePitch = MicDevice.Pitch;

			// Get the note number from the hertz value
			float midiNote = 12f * Mathf.Log(VoicePitch / 440f, 2f) + 69f;

			// Calculate the octave of the note
			VoiceOctave = (int) Mathf.Floor(midiNote / 12f);

			// Get the pitch (and disregard the note)
			VoiceNote = midiNote % 12f;

			// Set timing infos
			if (VoiceDetected) {
				TimeSinceVoiceDetected += Time.deltaTime;
				TimeSinceNoVoice = 0f;
			} else {
				TimeSinceNoVoice += Time.deltaTime;
				TimeSinceVoiceDetected = 0f;
			}

			// Activate starpower if loud!
			if (VoiceAmplitude > 8f && TimeSinceVoiceDetected < 0.5f) {
				CallStarpowerEvent();
			}
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<LyricInfo>) rawChart;
		}

		protected override void UpdateBotMode() {
			if (botChart == null) {
				return;
			}

			float songTime = Play.Instance.SongTime;

			// Get the next lyric
			while (botChart.Count > BotChartIndex && botChart[BotChartIndex].time <= songTime) {
				_botLyricInfo = botChart[BotChartIndex];
				BotChartIndex++;
			}

			// If we are past the lyric, null
			if (_botLyricInfo?.EndTime < songTime) {
				_botLyricInfo = null;
			}

			// Set info based on lyric
			if (_botLyricInfo == null) {
				VoiceAmplitude = -1f;
				TimeSinceNoVoice += Time.deltaTime;
				TimeSinceVoiceDetected = 0f;
			} else {
				VoiceAmplitude = 1f;
				TimeSinceVoiceDetected += Time.deltaTime;
				TimeSinceNoVoice = 0f;

				float timeIntoNote = Play.Instance.SongTime - _botLyricInfo.time;
				(VoicePitch, VoiceOctave) = _botLyricInfo.GetLerpedAndSplitNoteAtTime(timeIntoNote);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		protected override void UpdateNavigationMode() {
			NavigationEventForMapping(MenuAction.Confirm, CONFIRM);
			NavigationEventForMapping(MenuAction.Back, BACK);

			NavigationEventForMapping(MenuAction.Shortcut1, MENU_ACTION_1);
			NavigationEventForMapping(MenuAction.Shortcut2, MENU_ACTION_2);
			NavigationHoldableForMapping(MenuAction.Shortcut3, MENU_ACTION_3);

			NavigationEventForMapping(MenuAction.Up, UP);
			NavigationEventForMapping(MenuAction.Down, DOWN);

			if (WasMappingPressed(PAUSE)) {
				CallPauseEvent();
			}
		}

		public override Instrument[] GetAllowedInstruments() {
			return new Instrument[] {
				Instrument.VOCALS,
				Instrument.HARMONY,
			};
		}

		public override string GetTrackPath() {
			return null;
		}

		public override void ResetForSong() {
			base.ResetForSong();

			VoicePitch = default;
			VoiceAmplitude = default;
			VoiceNote = default;
			VoiceOctave = default;
			TimeSinceNoVoice = default;
			TimeSinceVoiceDetected = default;

			_botLyricInfo = null;
		}
	}
}