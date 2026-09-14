using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Minis
{
    /// <summary>
    /// A note input on a MIDI device.
    /// </summary>
    public class MidiNoteControl : ButtonControl
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<MidiNoteControl>("MidiNote");
        }

        public MidiNoteControl()
        {
            m_StateBlock.format = InputStateBlock.FormatBit;
            m_StateBlock.sizeInBits = 7;

            // ButtonControl parameters
            pressPoint = 1.0f / 127;
        }

        // Calculate note number from offset
        public int noteNumber => (int)stateOffsetRelativeToDeviceRoot;

        // Current velocity value; Returns zero when key off.
        public float velocity => ReadValue();
    }
}
