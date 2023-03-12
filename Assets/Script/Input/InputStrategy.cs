using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace YARG.Input {
	public abstract class InputStrategy {
		public bool botMode;
		protected int botChartIndex;

		public InputDevice inputDevice;
		protected Dictionary<string, InputControl> inputMappings;

		public int microphoneIndex = -1;

		public delegate void GenericCalibrationAction(InputStrategy inputStrategy);
		/// <summary>
		/// Gets invoked when the button for generic calibration is pressed.<br/>
		/// Make sure <see cref="UpdatePlayerMode"/> is being called.
		/// </summary>
		public event GenericCalibrationAction GenericCalibrationEvent;

		public delegate void StarpowerAction();
		/// <summary>
		/// Gets invoked when the button for generic starpower is pressed.
		/// </summary>
		public event StarpowerAction StarpowerEvent;

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
		}

		/// <returns>
		/// The input mapping keys that will be present in <see cref="inputMappings"/>
		/// </returns>
		public abstract string[] GetMappingNames();

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
		public abstract void UpdateBotMode(object chart, float songTime);

		/// <summary>
		/// Updates the navigation mode (menu mode) for this particular InputStrategy.
		/// </summary>
		public abstract void UpdateNavigationMode();

		protected void CallStarpowerEvent() {
			StarpowerEvent?.Invoke();
		}

		protected void CallGenericCalbirationEvent() {
			GenericCalibrationEvent?.Invoke(this);
		}

		protected void CallGenericNavigationEvent(NavigationType type, bool firstPressed) {
			GenericNavigationEvent?.Invoke(type, firstPressed);
		}

		public void CallGenericNavigationEventForButton(ButtonControl button, NavigationType type) {
			if (button?.wasPressedThisFrame ?? false) {
				CallGenericNavigationEvent(type, true);
			} else if (button?.isPressed ?? false) {
				CallGenericNavigationEvent(type, false);
			}
		}

		protected ButtonControl MappingAsButton(string key) {
			return inputMappings[key] as ButtonControl;
		}

		public InputControl GetMappingInputControl(string name) {
			return inputMappings[name];
		}

		public void SetMappingInputControl(string name, InputControl control) {
			inputMappings[name] = control;
		}
	}
}