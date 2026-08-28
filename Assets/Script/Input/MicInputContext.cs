using System.Collections.Generic;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Gameplay;

namespace YARG.Input
{
    public class MicInputContext
    {
        private readonly List<MicDevice> _devices;

        private readonly GameManager _gameManager;

        public MicInputContext(List<MicDevice> devices, GameManager gameManager)
        {
            _devices = devices;

            _gameManager = gameManager;
        }

        /// <summary>
        /// Starts recording the output.
        /// </summary>
        public void Start()
        {
            foreach (var device in _devices)
            {
                device.ClearOutputQueue();
                device.IsRecordingOutput = true;
            }
        }

        /// <summary>
        /// Gets the mic's input, converts it to an engine compatible format,
        /// then pushes the inputs to the <paramref name="engine"/>.
        /// </summary>
        public IEnumerable<GameInput> GetInputsFromMic()
        {
            foreach (var device in _devices)
            {
                while (device.DequeueOutputFrame(out var frame))
                {
                    // frame.VoiceDetected will ALWAYS be true here, as it wouldn't be queued otherwise

                    // Queue it up!
                    GameInput gameInput;
                    if (!frame.IsHit)
                    {
                        gameInput = GameInput.Create(frame.Time, VocalsAction.Pitch, frame.PitchAsMidiNote);
                    }
                    else
                    {
                        gameInput = GameInput.Create(frame.Time, VocalsAction.Hit, true);
                    }

                    yield return gameInput;
                }
            }
        }

        /// <summary>
        /// Stops recording the output.
        /// </summary>
        public void Stop()
        {
            foreach (var device in _devices)
            {
                device.IsRecordingOutput = false;
            }
        }
    }
}
