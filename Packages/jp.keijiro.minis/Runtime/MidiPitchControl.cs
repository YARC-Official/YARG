using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Minis
{
    /// <summary>
    /// The pitch bend input on a MIDI device.
    /// </summary>
    public class MidiPitchControl : AxisControl
    {
        private int _minValue = 0;
        private int _maxValue = 0;
        private int _zeroPoint = 0;

        internal static void Initialize()
        {
            InputSystem.RegisterLayout<MidiPitchControl>("MidiPitch");
        }

        public MidiPitchControl()
        {
            m_StateBlock.format = InputStateBlock.FormatBit;
            m_StateBlock.sizeInBits = 14;

            _minValue = 0;
            _maxValue = 0x3FFF;
            _zeroPoint = 0x2000;
        }

        public override unsafe float ReadUnprocessedValueFromState(void* statePtr)
        {
            int value = stateBlock.ReadInt(statePtr);
            return Normalize(value, _minValue, _maxValue, _zeroPoint);
        }

        // Borrowed from PlasticBand's IntegerAxisControl
        // Properly ensures 0-point value results in a value of 0f
        private static float Normalize(int value, int minValue, int maxValue, int zeroPoint)
        {
            if (value >= maxValue)
                return 1f;
            else if (value <= minValue)
                return minValue != zeroPoint ? -1f : 0f;

            int max;
            int min;
            float @base;
            if (value >= zeroPoint)
            {
                max = maxValue;
                min = zeroPoint;
                @base = 0f;
            }
            else
            {
                max = zeroPoint;
                min = minValue;
                @base = -1f;
            }

            float percentage;
            if (max == min) // Prevent divide-by-0
            {
                percentage = value >= max ? 1f : 0f;
            }
            else
            {
                percentage = (float) (value - min) / (max - min);
            }

            float normalized = @base + percentage;
            return normalized;
        }
    }
}
