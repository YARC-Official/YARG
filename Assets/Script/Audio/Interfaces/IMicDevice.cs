using System;
using UnityEngine;
using YARG.Settings;

namespace YARG.Audio
{
    public readonly struct MicOutputFrame
    {
        /// <summary>
        /// The time of the input.
        /// </summary>
        public readonly double Time;

        /// <summary>
        /// Pitch (in hertz) of the microphone.
        /// </summary>
        public readonly float Pitch;

        /// <summary>
        /// Volume (in dB) of the microphone.
        /// </summary>
        public readonly float Volume;

        /// <summary>
        /// Whether or not the microphone should be considered detected.
        /// </summary>
        public bool VoiceDetected => Volume > SettingsManager.Settings.MicrophoneSensitivity.Value;

        /// <summary>
        /// Gets the pitch as a MIDI note.
        /// </summary>
        public float PitchAsMidiNote => 12f * Mathf.Log(Pitch / 440f, 2f) + 69f;

        public MicOutputFrame(double time, float pitch, float volume)
        {
            Time = time;
            Pitch = pitch;
            Volume = volume;
        }
    }

    public interface IMicDevice : IDisposable
    {
        public const int RECORD_PERIOD_MS = 40;
        public const float UPDATES_PER_SECOND = 1000f / RECORD_PERIOD_MS;

        public string DisplayName { get; }
        public bool IsDefault { get; }

        public bool IsMonitoring { get; set; }
        public bool IsRecordingOutput { get; set; }

        /// <summary>
        /// Initialize the microphone.
        /// </summary>
        /// <returns>0 if successful, otherwise an error code.</returns>
        public int Initialize();

        /// <summary>
        /// Dequeues an output frame from the microphone.
        /// </summary>
        /// <returns>
        /// Whether the dequeue was successful.
        /// </returns>
        public bool DequeueOutputFrame(out MicOutputFrame frame);

        /// <summary>
        /// Clears the output queue.
        /// </summary>
        public void ClearOutputQueue();

        /// <summary>
        /// Set the monitoring level of this Microphone Device.
        /// </summary>
        /// <param name="volume">The volume to set to.</param>
        public void SetMonitoringLevel(float volume);

        /// <summary>
        /// Converts this microphone into its serialized form.
        /// </summary>
        public SerializedMic Serialize();

        /// <summary>
        /// Checks if the <see cref="SerializedMic"/> corresponds with this mic device.
        /// </summary>
        public bool IsSerializedMatch(SerializedMic mic);
    }
}