using YARG.Audio;
using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Input
{
    public class MicInputContext
    {
        public readonly IMicDevice Device;

        public MicInputContext(IMicDevice device)
        {
            Device = device;
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
                var gameInput = GameInput.Create(frame.Time, VocalsAction.Pitch, frame.PitchAsMidiNote);

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