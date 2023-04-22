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
				bool enabled = Enabled;
				if (enabled) {
					Disable();
				}

				_inputDevice = value;

				if (enabled) {
					Enable();
				}
			}
		}

		public int microphoneIndex = INVALID_MIC_INDEX;

		/// <summary>
		/// A list of the controls that correspond to each mapping.
		/// </summary>
		protected Dictionary<string, InputControl> inputMappings = new();
		/// <summary>
		/// A list of the states at the current and previous frame for each mapping.
		/// </summary>
		protected Dictionary<string, (bool previous, bool current)> inputStates = new();

		public bool Enabled { get; private set; }

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
			foreach (var key in GetMappingNames()) {
				inputMappings.Add(key, null);
				inputStates.Add(key, (false, false));
			}
		}

		public void Enable() {
			// Bind events
			GameManager.OnUpdate += EventUpdateLoop;

			// Temporary for MIDI
			if (_inputDevice is MidiDevice newMidi) {
				newMidi.onWillNoteOn += OnWillNoteOn;
				newMidi.onWillNoteOff += OnWillNoteOff;
			}

			Enabled = true;
		}

		public void Disable() {
			// Unbind events
			GameManager.OnUpdate -= EventUpdateLoop;

			// Temporary for MIDI
			if (_inputDevice is MidiDevice oldMidi) {
				oldMidi.onWillNoteOn -= OnWillNoteOn;
				oldMidi.onWillNoteOff -= OnWillNoteOff;
			}

			Enabled = false;
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
		/// Initializes the bot mode for this particular InputStrategy.
		/// </summary>
		/// <param name="chart">A reference to the current chart.</param>
		public abstract void InitializeBotMode(object chart);

		/// <summary>
		/// Updates the player mode (normal mode) for this particular InputStrategy.
		/// </summary>
		protected abstract void UpdatePlayerMode();

		/// <summary>
		/// Updates the bot mode for this particular InputStrategy.
		/// </summary>
		protected abstract void UpdateBotMode();

		/// <summary>
		/// Updates the navigation mode (menu mode) for this particular InputStrategy.
		/// </summary>
		protected abstract void UpdateNavigationMode();

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
			// Update previous and current states
			foreach (var mapping in inputMappings) {
				bool previous = inputStates[mapping.Key].current;
				inputStates[mapping.Key] = (previous, IsControlPressed(mapping.Value));
			}

			// Update inputs
			UpdateNavigationMode();
			if (botMode) {
				UpdateBotMode();
			} else {
				UpdatePlayerMode();
			}
		}

		// Temporary for MIDI, as very brief note events are likely to be missed otherwise
		private void OnWillNoteOn(MidiNoteControl midi, float velocity) {
			foreach (var input in inputMappings) {
				if (input.Value == midi && !WasMappingReleased(input.Key)) {
					// Set current state to active
					bool previous = inputStates[input.Key].previous;
					inputStates[input.Key] = (previous, true);
					return;
				}
			}
		}

		private void OnWillNoteOff(MidiNoteControl midi) {
			foreach (var input in inputMappings) {
				if (input.Value == midi && !WasMappingPressed(input.Key)) {
					// Set current state to inactive
					bool previous = inputStates[input.Key].previous;
					inputStates[input.Key] = (previous, false);
					return;
				}
			}
		}

		public static bool IsControlPressed(InputControl control) {
			if (control is ButtonControl button) {
				return button.isPressed;
			}

			return false;
		}

		protected bool IsMappingPressed(string key) {
			var control = inputMappings[key];
			return IsControlPressed(control);
		}

		protected bool WasMappingPressed(string key) {
			var (previous, current) = inputStates[key];
			return !previous && current;
		}

		protected bool WasMappingReleased(string key) {
			var (previous, current) = inputStates[key];
			return previous && !current;
		}

		public InputControl GetMappingInputControl(string name) {
			return inputMappings[name];
		}

		public void SetMappingInputControl(string name, InputControl control) {
			inputMappings[name] = control;
		}
	}
}