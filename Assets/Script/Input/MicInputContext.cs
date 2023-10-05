using YARG.Audio;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay;

namespace YARG.Input
{
    public class MicInputContext
    {
        public readonly IMicDevice Device;

        private readonly GameManager _gameManager;

        public MicInputContext(IMicDevice device, GameManager gameManager)
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
        public void PushInputsToEngine(BaseEngine engine)
        {
            while (Device.DequeueOutputFrame(out var frame))
            {
                // frame.VoiceDetected will ALWAYS be true here, as it wouldn't be queued otherwise

                // Create the GameInput
                double time = _gameManager.GetRelativeInputTime(frame.Time);
                var gameInput = GameInput.Create(time, VocalsAction.Pitch, frame.PitchAsMidiNote);

                // Queue it up!
                engine.QueueInput(gameInput);
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