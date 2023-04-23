using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input {
	public abstract class InputStrategy {
		protected class StrategyControl {
			public InputControl control;
			public (bool previous, bool current) state;
		}

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
		protected Dictionary<string, StrategyControl> inputMappings = new();

		public bool Enabled { get; private set; }

		private IDisposable eventListener = null;

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

		public delegate void GenericNavigationAction(NavigationType navigationType, bool pressed);
		/// <summary>
		/// Gets invoked when any generic navigation button is pressed.<br/>
		/// Make sure <see cref="UpdateNavigationMode"/> is being called.
		/// </summary>
		public event GenericNavigationAction GenericNavigationEvent;

		public InputStrategy() {
			// Add keys for each input mapping
			foreach (var key in GetMappingNames()) {
				inputMappings.Add(key, new());
			}
		}

		public void Enable() {
			// Bind events
			GameManager.OnUpdate += OnUpdate;
			if (_inputDevice != null) {
				eventListener = InputSystem.onEvent.ForDevice(_inputDevice).Call(OnInputEvent);
			}

			Enabled = true;
		}

		public void Disable() {
			// Unbind events
			GameManager.OnUpdate -= OnUpdate;
			eventListener?.Dispose();
			eventListener = null;

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

		protected void CallGenericNavigationEvent(NavigationType type, bool pressed) {
			GenericNavigationEvent?.Invoke(type, pressed);
		}

		public void CallGenericNavigationEventForButton(string key, NavigationType type) {
			if (WasMappingPressed(key)) {
				CallGenericNavigationEvent(type, true);
			} else if (WasMappingReleased(key)) {
				CallGenericNavigationEvent(type, false);
			}
		}

		private void OnUpdate() {
			if (botMode) {
				UpdateBotMode();
			}
		}

		private void OnInputEvent(InputEventPtr eventPtr) {
			// Only take state events
			if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) {
				return;
			}

			// Update previous and current states
			foreach (var mapping in inputMappings.Values) {
				// Ignore unmapped controls
				if (mapping.control == null) {
					continue;
				}

				// Progress state history forward
				mapping.state.previous = mapping.state.current;
				if (mapping.control.HasValueChangeInEvent(eventPtr)) {
					// Don't check pressed state unless there was a value change
					// There seems to be an issue with delta state events (which MIDI devices use) where
					// a control that wasn't changed in that event will report the wrong value
					mapping.state.current = IsControlPressed(mapping.control, eventPtr);
				}
			}

			// Update inputs
			UpdateNavigationMode();
			UpdatePlayerMode();
		}

		/// <summary>
		/// Forces the input strategy to update its inputs. This is used for microphone input.
		/// </summary>
		public void ForceUpdateInputs() {
			UpdateNavigationMode();
			UpdatePlayerMode();
		}

		public static bool IsControlPressed(InputControl control) {
			if (control is ButtonControl button) {
				return button.isPressed;
			}

			return false;
		}

		public static bool IsControlPressed(InputControl control, InputEventPtr eventPtr) {
			if (control is ButtonControl button) {
				return button.IsValueConsideredPressed(button.ReadValueFromEvent(eventPtr));
			}

			return false;
		}

		protected bool IsMappingPressed(string key) {
			return inputMappings[key].state.current;
		}

		protected bool WasMappingPressed(string key) {
			var (previous, current) = inputMappings[key].state;
			return !previous && current;
		}

		protected bool WasMappingReleased(string key) {
			var (previous, current) = inputMappings[key].state;
			return previous && !current;
		}

		public InputControl GetMappingInputControl(string name) {
			return inputMappings[name].control;
		}

		public void SetMappingInputControl(string name, InputControl control) {
			inputMappings[name].control = control;
		}
	}
}