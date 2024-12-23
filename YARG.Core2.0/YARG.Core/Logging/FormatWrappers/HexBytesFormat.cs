using System;
using Cysharp.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Logging
{
    /// <summary>
    /// A wrapper around a byte array which formats the array as a hex string.
    /// </summary>
    public struct HexBytesFormat
    {
        private byte[] _data;
        private bool _dashes;

        static HexBytesFormat()
        {
            Utf16ValueStringBuilder.RegisterTryFormat<HexBytesFormat>(TryFormat);
        }

        public HexBytesFormat(byte[] data, bool dashes = true)
        {
            _data = data;
            _dashes = dashes;
        }

        private static bool TryFormat(HexBytesFormat value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)
            => value.TryFormat(destination, out charsWritten);

        public readonly bool TryFormat(Span<char> destination, out int charsWritten) //, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
            => _data.TryFormatHex(destination, out charsWritten, _dashes);

        public readonly override string ToString()
            => _data.ToHexString(_dashes);
    }
}