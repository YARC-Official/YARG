using System;
using System.Collections.Generic;
using Minis;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YARG.Input {
	public abstract class InputStrategy {
		public const float PRESS_THRESHOLD = 0.75f; // TODO: Remove once control calibration is added
		public const int INVALID_MIC_INDEX = -1;

		public bool botMode;
		protected int botChartIndex;

		private InputDevice _inputDevice;
		public InputDevice InputDevice {
			get => _inputDevice;
			set {
				// Temporary for MIDI

				// Unbind previous
				if (_inputDevice is MidiDevice oldMidi) {
					oldMidi.onWillNoteOn -= OnWillNoteOn;
					oldMidi.onWillNoteOff -= OnWillNoteOff;
				}

				_inputDevice = value;

				// Bind new
				if (_inputDevice is MidiDevice newMidi) {
					newMidi.onWillNoteOn += OnWillNoteOn;
					newMidi.onWillNoteOff += OnWillNoteOff;
				}
			}
		}

		public int microphoneIndex = INVALID_MIC_INDEX;

		// Temporary for MIDI
		private OccurrenceList<string> midiPressed = new();
		private OccurrenceList<string> midiReleased = new();

		protected Dictionary<string, InputControl> inputMappings;

		public delegate void GenericCalibrationAction(InputStrategy inputStrategy);
		/// <summary>
		/// Gets invoked when the button for generic calibration is pressed.<br/>
		/// Make sure <see cref="UpdatePlayerMode"/> is being called.
		/// </summary>
		public event GenericCalibrationAction GenericCalibrationEvent;

		/// <summary>
		/// Gets invoked when the button for generic starpower is pressed.
		/// </summary>
		public event Action<InputStrategy> StarpowerEvent;

		/// <summary>
		/// Gets invoked when the button for generic pause is pressed.
		/// </summary>
		public event Action PauseEvent;

		public delegate void GenericNavigationAction(NavigationType navigationType, bool firstPressed);
		/// <summary>
		/// Gets invoked when any generic navigation button is pressed.<br/>
		/// Make sure <see cref="UpdateNavigationMode"/> is being called.
		/// </summary>
		public event GenericNavigationAction GenericNavigationEvent;

		public InputStrategy() {
			// Add keys for each input mapping
			inputMappings = new();
			foreach (var key in GetMappingNames()) {
				inputMappings.Add(key, null);
			}

			// Bind events
			GameManager.OnUpdate += EventUpdateLoop;
		}

		~InputStrategy() {
			// Force unbind
			InputDevice = null;

			// Unbind events
			GameManager.OnUpdate -= EventUpdateLoop;
		}

		/// <returns>
		/// The input mapping keys that will be present in <see cref="inputMappings"/>
		/// </returns>
		public abstract string[] GetMappingNames();

		/// <returns>
		/// An array of the allow instruments for the input strategy.
		/// </returns>
		public abstract string[] GetAllowedInstruments();

		/// <returns>
		/// The path of the track addressable.
		/// </returns>
		public abstract string GetTrackPath();

		/// <summary>
		/// Resets the InputStrategy for a new song.
		/// </summary>
		public virtual void ResetForSong() {
			botChartIndex = 0;
		}

		/// <summary>
		/// Updates the player mode (normal mode) for this particular InputStrategy.
		/// </summary>
		public abstract void UpdatePlayerMode();

		/// <summary>
		/// Updates the bot mode for this particular InputStrategy.
		/// </summary>
		/// <param name="chart">A reference to the current chart.</param>
		/// <param name="songTime">The song time in seconds.</param>
		/// <param name="chosenInstrument">The instrument that the bot is playing.</param>
		public abstract void UpdateBotMode(object chart, float songTime);

		/// <summary>
		/// Updates the navigation mode (menu mode) for this particular InputStrategy.
		/// </summary>
		public abstract void UpdateNavigationMode();

		protected void CallStarpowerEvent() {
			StarpowerEvent?.Invoke(this);
		}

		protected void CallPauseEvent() {
			PauseEvent?.Invoke();
		}

		protected void CallGenericCalbirationEvent() {
			GenericCalibrationEvent?.Invoke(this);
		}

		protected void CallGenericNavigationEvent(NavigationType type, bool firstPressed) {
			GenericNavigationEvent?.Invoke(type, firstPressed);
		}

		public void CallGenericNavigationEventForButton(string key, NavigationType type) {
			if (WasMappingPressed(key)) {
				CallGenericNavigationEvent(type, true);
			} else {
				var input = GetMappingInputControl(key);

				if (input is MidiNoteControl) {
					return;
				}

				if (input is ButtonControl button && button.isPressed) {
					CallGenericNavigationEvent(type, false);
				}
			}
		}

		private void EventUpdateLoop() {
			// THIS IS TEMPORARY
			// as Minis has an issue

			foreach (var pressed in midiPressed.ToDictionary()) {
				if (pressed.Value >= 2) {
					midiPressed.RemoveAll(pressed.Key, true);
				} else {
					midiPressed.Add(pressed.Key);
				}
			}

			foreach (var released in midiReleased.ToDictionary()) {
				if (released.Value >= 2) {
					midiReleased.RemoveAll(released.Key, true);
				} else {
					midiReleased.Add(released.Key);
				}
			}
		}

		private void OnWillNoteOn(MidiNoteControl midi, float velocity) {
			foreach (var input in inputMappings) {
				if (input.Value == midi) {
					midiPressed.Add(input.Key);
					return;
				}
			}
		}

		private void OnWillNoteOff(MidiNoteControl midi) {
			foreach (var input in inputMappings) {
				if (input.Value == midi) {
					midiReleased.Add(input.Key);
					return;
				}
			}
		}

		protected bool WasMappingPressed(string key) {
			var mapping = inputMappings[key];

			if (mapping is MidiNoteControl) {
				return midiPressed.GetCount(key) >= 1;
			} else if (mapping is ButtonControl button) {
				return button.wasPressedThisFrame;
			}

			return false;
		}

		protected bool WasMappingReleased(string key) {
			var mapping = inputMappings[key];

			if (mapping is MidiNoteControl) {
				return midiReleased.GetCount(key) >= 1;
			} else if (mapping is ButtonControl button) {
				return button.wasReleasedThisFrame;
			}

			return false;
		}

		public InputControl GetMappingInputControl(string name) {
			return inputMappings[name];
		}

		public void SetMappingInputControl(string name, InputControl control) {
			inputMappings[name] = control;
		}
	}
}