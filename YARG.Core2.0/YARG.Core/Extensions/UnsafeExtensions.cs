using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Extensions
{
    // Not actually extensions, just backported functions from Unsafe and BitConverter
    public static class UnsafeExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TTo BitCast<TFrom, TTo>(TFrom source)
            where TFrom : struct
            where TTo : struct
        {
            if (Unsafe.SizeOf<TFrom>() != Unsafe.SizeOf<TTo>())
                throw new NotSupportedException("Cannot cast between types of different sizes.");

            return Unsafe.ReadUnaligned<TTo>(ref Unsafe.As<TFrom, byte>(ref source));
        }

        public static ulong DoubleToUInt64Bits(double value)
        {
            return BitCast<double, ulong>(value);
        }

        public static double UInt64BitsToDouble(ulong value)
        {
            return BitCast<ulong, double>(value);
        }
    }
}