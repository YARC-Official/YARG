using System;
using YARG.Core.Extensions;

namespace YARG.Core.Utility
{
    using TrimSplitter = SpanSplitter<char, TrimSplitProcessor>;
    using AsciiTrimSplitter = SpanSplitter<char, AsciiTrimSplitProcessor>;
    using Latin1TrimSplitter = SpanSplitter<char, Latin1TrimSplitProcessor>;

    /// <summary>
    /// Enumerates a <see cref="ReadOnlySpan{T}"/>, splitting based on a specific value of <typeparamref name="T"/>
    /// and using <typeparamref name="TSplitter"/> to refine the resulting split value.
    /// </summary>
    public ref struct SpanSplitter<T, TSplitter>
        where T : IEquatable<T>
        where TSplitter : ISplitProcessor<T>, new()
    {
        private readonly ReadOnlySpan<T> _original;
        private ReadOnlySpan<T> _remaining;
        private readonly T _split;
        private readonly TSplitter _splitter;

        public ReadOnlySpan<T> Current { get; private set; }
        public readonly ReadOnlySpan<T> CurrentToEnd
        {
            get
            {
                int fromEnd = _remaining.IsEmpty ? Current.Length
                    : Current.Length + 1 + _remaining.Length; // + 1 to account for split being skipped
                int index = _original.Length - fromEnd;
                return _original[index..];
            }
        }

        public readonly ReadOnlySpan<T> Original => _original;
        public readonly ReadOnlySpan<T> Remaining => _remaining;
        public readonly T Split => _split;

        public SpanSplitter(ReadOnlySpan<T> buffer, T split)
        {
            _original = buffer;
            _remaining = buffer;
            _split = split;
            _splitter = new();
            Current = ReadOnlySpan<T>.Empty;
        }

        public readonly SpanSplitter<T, TSplitter> GetEnumerator() => this;

        public ReadOnlySpan<T> GetNext() => MoveNext() ? Current : ReadOnlySpan<T>.Empty;

        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
                return false;

            Current = _remaining.SplitOnce(_split, _splitter, out _remaining);
            return !Current.IsEmpty;
        }

        public void Reset()
        {
            Current = ReadOnlySpan<T>.Empty;
            _remaining = _original;
        }
    }

    public interface ISplitProcessor<T>
        where T : IEquatable<T>
    {
        ReadOnlySpan<T> GetSegment(ReadOnlySpan<T> buffer, int splitIndex);
        ReadOnlySpan<T> GetRemaining(ReadOnlySpan<T> buffer, int splitIndex);
    }

    public readonly struct SpanSplitProcessor<T> : ISplitProcessor<T>
        where T : IEquatable<T>
    {
        public readonly ReadOnlySpan<T> GetSegment(ReadOnlySpan<T> buffer, int splitIndex)
            => buffer[..splitIndex];
        public readonly ReadOnlySpan<T> GetRemaining(ReadOnlySpan<T> buffer, int splitIndex)
            => buffer[splitIndex..];
    }

    public readonly struct TrimSplitProcessor : ISplitProcessor<char>
    {
        public readonly ReadOnlySpan<char> GetSegment(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[..splitIndex].Trim();
        public readonly ReadOnlySpan<char> GetRemaining(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[splitIndex..].Trim();
    }

    public readonly struct AsciiTrimSplitProcessor : ISplitProcessor<char>
    {
        public readonly ReadOnlySpan<char> GetSegment(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[..splitIndex].TrimAscii();
        public readonly ReadOnlySpan<char> GetRemaining(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[splitIndex..].TrimAscii();
    }

    public readonly struct Latin1TrimSplitProcessor : ISplitProcessor<char>
    {
        public readonly ReadOnlySpan<char> GetSegment(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[..splitIndex].TrimLatin1();
        public readonly ReadOnlySpan<char> GetRemaining(ReadOnlySpan<char> buffer, int splitIndex)
            => buffer[splitIndex..].TrimLatin1();
    }

    public static class SpanSplitterExtensions
    {
        public static SpanSplitter<char, SpanSplitProcessor<char>> SplitAsSpan(this string buffer, char split)
            => new(buffer, split);

        public static SpanSplitter<T, SpanSplitProcessor<T>> Split<T>(this Span<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);

        public static SpanSplitter<T, SpanSplitProcessor<T>> Split<T>(this ReadOnlySpan<T> buffer, T split)
            where T : IEquatable<T>
            => new(buffer, split);

        public static TrimSplitter SplitTrimmed(this string buffer, char split)
            => new(buffer, split);

        public static TrimSplitter SplitTrimmed(this ReadOnlySpan<char> buffer, char split)
            => new(buffer, split);

        public static AsciiTrimSplitter SplitTrimmedAscii(this string buffer, char split)
            => new(buffer, split);

        public static AsciiTrimSplitter SplitTrimmedAscii(this ReadOnlySpan<char> buffer, char split)
            => new(buffer, split);

        public static Latin1TrimSplitter SplitTrimmedLatin1(this string buffer, char split)
            => new(buffer, split);

        public static Latin1TrimSplitter SplitTrimmedLatin1(this ReadOnlySpan<char> buffer, char split)
            => new(buffer, split);

        public static ReadOnlySpan<char> SplitOnce(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnce(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<T> SplitOnce<T>(this Span<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            => SplitOnce((ReadOnlySpan<T>) buffer, split, out remaining);

        public static ReadOnlySpan<T> SplitOnce<T>(this ReadOnlySpan<T> buffer, T split, out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            => buffer.SplitOnce(split, new SpanSplitProcessor<T>(), out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmed(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnceTrimmed(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmed(this ReadOnlySpan<char> buffer, char split, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnce(split, new TrimSplitProcessor(), out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmedAscii(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnceTrimmedAscii(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmedAscii(this ReadOnlySpan<char> buffer, char split, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnce(split, new AsciiTrimSplitProcessor(), out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmedLatin1(this string buffer, char split, out ReadOnlySpan<char> remaining)
            => SplitOnceTrimmedLatin1(buffer.AsSpan(), split, out remaining);

        public static ReadOnlySpan<char> SplitOnceTrimmedLatin1(this ReadOnlySpan<char> buffer, char split, out ReadOnlySpan<char> remaining)
            => buffer.SplitOnce(split, new Latin1TrimSplitProcessor(), out remaining);

        public static ReadOnlySpan<T> SplitOnce<T, TSplitter>(this ReadOnlySpan<T> buffer, T split, TSplitter splitter,
            out ReadOnlySpan<T> remaining)
            where T : IEquatable<T>
            where TSplitter : ISplitProcessor<T>
        {
            remaining = buffer;
            var result = ReadOnlySpan<T>.Empty;

            // For ignoring empty splits
            while (result.IsEmpty && !remaining.IsEmpty)
            {
                int splitIndex = remaining.IndexOf(split);
                if (splitIndex < 0)
                    splitIndex = remaining.Length;

                // Split on the value
                result = splitter.GetSegment(remaining, splitIndex);

                // Skip the split value and ignore consecutive split values
                while (splitIndex < remaining.Length && remaining[splitIndex].Equals(split))
                    splitIndex++;

                remaining = splitter.GetRemaining(remaining, splitIndex);
            }

            return result;
        }
    }
}