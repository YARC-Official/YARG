using UnityEngine;
using YARG.Audio;

namespace YARG.Player.Input
{
    public sealed class MicInput
    {
        public IMicDevice MicDevice { get; private set; }

        public float VoiceAmplitude { get; private set; }
        public float VoiceNote { get; private set; }
        public int VoiceOctave { get; private set; }
        public bool VoiceDetected { get; private set; }
        public float TimeSinceNoVoice { get; private set; }
        public float TimeSinceVoiceDetected { get; private set; }

        public MicInput(IMicDevice micDevice)
        {
            MicDevice = micDevice;
        }

        private void OnUpdate()
        {
            // Set info from mic
            VoiceDetected = MicDevice.VoiceDetected;
            VoiceAmplitude = MicDevice.Amplitude;

            // Get the note number from the hertz value
            float note = 12f * Mathf.Log(MicDevice.Pitch / 440f, 2f) + 69f;

            // Calculate the octave of the note
            VoiceOctave = (int) Mathf.Floor(note / 12f);

            // Get the pitch (and disregard the note)
            VoiceNote = note % 12f;

            // Set timing infos
            if (VoiceDetected)
            {
                TimeSinceVoiceDetected += Time.deltaTime;
                TimeSinceNoVoice = 0f;
            }
            else
            {
                TimeSinceNoVoice += Time.deltaTime;
                TimeSinceVoiceDetected = 0f;
            }

            // Activate starpower if loud!
            // if (VoiceAmplitude > 8f && TimeSinceVoiceDetected < 0.5f)
            // {
            //     CallStarpowerEvent();
            // }
        }

        public void ResetInfo()
        {
            VoiceAmplitude = default;
            VoiceNote = default;
            VoiceOctave = default;
            TimeSinceNoVoice = default;
            TimeSinceVoiceDetected = default;
        }
    }
}