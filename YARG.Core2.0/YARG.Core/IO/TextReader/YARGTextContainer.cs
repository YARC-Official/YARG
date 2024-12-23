using System;
using System.Text;

namespace YARG.Core.IO
{
    public unsafe struct YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
    {
        public static readonly YARGTextContainer<TChar> Null = new()
        {
            Encoding = null!,
            Position = null,
        };

        public readonly TChar* End;
        public Encoding Encoding;
        public TChar* Position;

        public readonly TChar CurrentValue
        {
            get
            {
                if (Position < End)
                {
                    return *Position;
                }
                throw new InvalidOperationException("End of file reached");
            }
        }

        public readonly bool IsActive => End != null;

        public YARGTextContainer(in FixedArray<TChar> buffer, Encoding encoding)
        {
            Position = buffer.Ptr;
            End = buffer.Ptr + buffer.Length;
            Encoding = encoding;
        }

        public readonly bool IsCurrentCharacter(int cmp)
        {
            return Position->ToInt32(null).Equals(cmp);
        }

        public readonly bool IsAtEnd()
        {
            return Position >= End;
        }
    }
}
