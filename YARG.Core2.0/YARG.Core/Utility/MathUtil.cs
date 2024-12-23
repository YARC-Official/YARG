using YARG.Core.Extensions;

namespace YARG.Core.Utility
{
    public static class MathUtil
    {
        // Backported from newer .NET
        public static double BitIncrement(double x)
        {
            ulong bits = UnsafeExtensions.DoubleToUInt64Bits(x);

            const ulong negativeZeroBits = 0x8000_0000_0000_0000;
            const ulong negativeInfinityBits = 0xFFF0_0000_0000_0000;

            if (!double.IsFinite(x))
            {
                return bits == negativeInfinityBits ? double.MinValue : x;
            }

            if (bits == negativeZeroBits)
            {
                return double.Epsilon;
            }

            if (double.IsNegative(x))
            {
                bits -= 1;
            }
            else
            {
                bits += 1;
            }

            return UnsafeExtensions.UInt64BitsToDouble(bits);
        }
    }
}