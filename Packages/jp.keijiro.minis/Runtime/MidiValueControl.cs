using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Minis
{
    /// <summary>
    /// A control input on a MIDI device.
    /// </summary>
    public class MidiValueControl : AxisControl
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<MidiValueControl>("MidiValue");
        }

        public MidiValueControl()
        {
            m_StateBlock.format = InputStateBlock.FormatBit;
            m_StateBlock.sizeInBits = 7;
        }

        // Calculate control number from offset
        public int controlNumber => (int)stateOffsetRelativeToDeviceRoot - 128;
    }
}
