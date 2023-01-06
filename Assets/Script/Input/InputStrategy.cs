using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using YARG.Data;

namespace YARG.Input {
	public abstract class InputStrategy {
		public bool botMode;
		protected int botChartIndex;

		public InputDevice inputDevice;
		protected Dictionary<string, InputControl> inputMappings;

		public delegate void GenericCalibrationAction(InputStrategy inputStrategy);
		public event GenericCalibrationAction GenericCalibrationEvent;

		public delegate void StarpowerAction();
		public event StarpowerAction StarpowerEvent;

		public InputStrategy(InputDevice inputDevice, bool botMode) {
			this.botMode = botMode;
			this.inputDevice = inputDevice;

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
		public abstract void UpdateBotMode(List<NoteInfo> chart, float songTime);

		protected void CallStarpowerEvent() {
			StarpowerEvent?.Invoke();
		}

		protected void CallGenericCalbirationEvent() {
			GenericCalibrationEvent?.Invoke(this);
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