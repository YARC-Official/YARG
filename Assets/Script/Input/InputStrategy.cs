using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Input {
	public abstract class InputStrategy {
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
		protected Dictionary<string, ControlBinding> inputMappings;
		public IReadOnlyDictionary<string, ControlBinding> Mappings => inputMappings;

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
			// Initialize mappings
			inputMappings = GetMappings();
			// Set up debounce overrides
			foreach (var mapping in inputMappings.Values) {
				string overrideKey = mapping.DebounceOverrideKey;
				if (overrideKey != null && inputMappings.TryGetValue(overrideKey, out var overrideMapping)) {
					mapping.DebounceOverrideBinding = overrideMapping;
				}
			}
		}

		public void Enable() {
			if (Enabled) {
				return;
			}

			// Bind events
			InputSystem.onAfterUpdate += OnUpdate;
			if (_inputDevice != null) {
				eventListener = InputSystem.onEvent.ForDevice(_inputDevice).Call(OnInputEvent);
			}

			Enabled = true;
		}

		public void Disable() {
			if (!Enabled) {
				return;
			}

			// Unbind events
			InputSystem.onAfterUpdate -= OnUpdate;
			eventListener?.Dispose();
			eventListener = null;

			Enabled = false;
		}

		/// <returns>
		/// The input mapping keys that will be present in <see cref="inputMappings"/>
		/// </returns>
		protected virtual Dictionary<string, ControlBinding> GetMappings() => new();

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

		protected virtual void OnUpdate() {
			if (botMode) {
				UpdateBotMode();
				return;
			}

			// Update mapping debouncing
			bool stateUpdated = false;
			foreach (var mapping in inputMappings.Values) {
				stateUpdated |= mapping.UpdateDebounce();
			}

			// Update inputs if necessary
			if (stateUpdated) {
				UpdateNavigationMode();
				UpdatePlayerMode();
			}
		}

		private void OnInputEvent(InputEventPtr eventPtr) {
			// Only take state events
			if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) {
				return;
			}

			// Update mapping states
			foreach (var mapping in inputMappings.Values) {
				mapping.UpdateState(eventPtr);
			}

			// Update inputs
			UpdateNavigationMode();
			UpdatePlayerMode();
		}

		/// <summary>
		/// Forces the input strategy to update.
		/// </summary>
		public void ForceUpdate() {
			UpdateNavigationMode();
			UpdatePlayerMode();
			OnUpdate();
		}

		public static bool IsControlPressed(InputControl<float> control) {
			if (control is ButtonControl button) {
				return button.isPressed;
			}

			return control.IsActuated(ControlBinding.DEFAULT_PRESS_THRESHOLD);
		}

		public static bool IsControlPressed(InputControl<float> control, InputEventPtr eventPtr) {
			if (control is ButtonControl button) {
				return button.IsValueConsideredPressed(button.ReadValueFromEvent(eventPtr));
			}
	
			return control.ReadValueFromEvent(eventPtr) >= ControlBinding.DEFAULT_PRESS_THRESHOLD;
		}

		protected bool IsMappingPressed(string key) {
			return inputMappings[key].IsPressed();
		}

		protected bool WasMappingPressed(string key) {
			return inputMappings[key].WasPressed();
		}

		protected bool WasMappingReleased(string key) {
			return inputMappings[key].WasReleased();
		}

		protected float GetMappingValue(string key) {
			return inputMappings[key].State.current;
		}

		protected float GetPreviousMappingValue(string key) {
			return inputMappings[key].State.previous;
		}

		public InputControl<float> GetMappingInputControl(string name) {
			return inputMappings[name].Control;
		}

		public void SetMappingInputControl(string name, InputControl<float> control) {
			inputMappings[name].Control = control;
		}
	}
}