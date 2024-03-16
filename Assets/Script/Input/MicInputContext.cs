using System.Collections.Generic;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Gameplay;

namespace YARG.Input
{
    public class MicInputContext
    {
        public readonly MicDevice Device;

        private readonly GameManager _gameManager;

        public MicInputContext(MicDevice device, GameManager gameManager)
        {
            Device = device;

            _gameManager = gameManager;
        }

        /// <summary>
        /// Starts recording the output.
        /// </summary>
        public void Start()
        {
            Device.ClearOutputQueue();
            Device.IsRecordingOutput = true;
        }

        /// <summary>
        /// Gets the mic's input, converts it to an engine compatible format,
        /// then pushes the inputs to the <paramref name="engine"/>.
        /// </summary>
        public IEnumerable<GameInput> GetInputsFromMic()
        {
            while (Device.DequeueOutputFrame(out var frame))
            {
                // frame.VoiceDetected will ALWAYS be true here, as it wouldn't be queued otherwise

                // Queue it up!
                var gameInput = GameInput.Create(frame.Time, VocalsAction.Pitch, frame.PitchAsMidiNote);
                yield return gameInput;
            }
        }

        /// <summary>
        /// Stops recording the output.
        /// </summary>
        public void Stop()
        {
            Device.IsRecordingOutput = false;
        }
    }
}