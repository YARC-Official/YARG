using System;

namespace YARG.Audio {
	public interface IMicDevice : IDisposable {
		public float PitchUpdatesPerSecond { get; }

		public string DisplayName { get; }
		public bool IsDefault { get; }

		public bool IsMonitoring { get; set; }

		/// <summary>
		/// The current pitch of this Microphone Device.
		/// </summary>
		public float Pitch { get; }

		/// <summary>
		/// The current amplitude of this Microphone Device.
		/// </summary>
		public float Amplitude { get; }

		/// <summary>
		/// Whether or not a voice/singing is detected.
		/// </summary>
		public bool VoiceDetected { get; }

		/// <summary>
		/// Initialize the microphone device with the given device number.
		/// </summary>
		/// <param name="device">The device number to associate with this Microphone Device.</param>
		/// <returns>0 if successful, otherwise an error code.</returns>
		public int Initialize();

		/// <summary>
		/// Set the monitoring level of this Microphone Device.
		/// </summary>
		/// <param name="volume">The volume to set to.</param>
		public void SetMonitoringLevel(float volume);

	}
}