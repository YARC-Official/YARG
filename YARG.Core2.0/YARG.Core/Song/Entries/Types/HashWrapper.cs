using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    [Serializable]
    public unsafe struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();

        public const int HASH_SIZE_IN_BYTES = 20;
        public const int HASH_SIZE_IN_INTS = HASH_SIZE_IN_BYTES / sizeof(int);

        private fixed int _hash[HASH_SIZE_IN_INTS];

        public readonly byte[] HashBytes
        {
            get
            {
                var bytes = new byte[HASH_SIZE_IN_BYTES];
                fixed (byte* ptr = bytes)
                {
                    int* integers = (int*)ptr;
                    for (var i = 0; i < HASH_SIZE_IN_INTS; i++)
                    {
                        integers[i] = _hash[i];
                    }
                }
                return bytes;
            }
        }

        public static HashWrapper Deserialize(Stream stream)
        {
            var wrapper = new HashWrapper();
            var span = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);
            if (stream.Read(span) != HASH_SIZE_IN_BYTES)
            {
                throw new EndOfStreamException();
            }
            return wrapper;
        }

        public static HashWrapper Hash(ReadOnlySpan<byte> span)
        {
            var wrapper = new HashWrapper();
            var hashSpan = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);

            using var algo = Algorithm;
            if (!algo.TryComputeHash(span, hashSpan, out int written))
            {
                throw new Exception("fucking how??? Hash generation error");
            }
            return wrapper;
        }

        public static HashWrapper Create(ReadOnlySpan<byte> hash)
        {
            var wrapper = new HashWrapper();
            var span = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);
            hash.CopyTo(span);
            return wrapper;
        }

        public static HashWrapper FromString(ReadOnlySpan<char> str)
        {
            var wrapper = new HashWrapper();
            try
            {
                var bytes = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);
                for (int i = 0; i < HASH_SIZE_IN_BYTES; i++)
                {
                    // Each set of 2 characters represents 1 byte
                    var slice = str.Slice(i *  2, 2);
                    bytes[i]= byte.Parse(slice, NumberStyles.AllowHexSpecifier);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to read hash");
            }
            return wrapper;
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            fixed (int* values = _hash)
            {
                var bytes = new Span<byte>(values, HASH_SIZE_IN_BYTES);
                writer.Write(bytes);
            }
        }

        public readonly void Serialize(Stream stream)
        {
            fixed (int* values = _hash)
            {
                var bytes = new Span<byte>(values, HASH_SIZE_IN_BYTES);
                stream.Write(bytes);
            }
        }

        public readonly int CompareTo(HashWrapper other)
        {
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                if (_hash[i] != other._hash[i])
                {
                    return _hash[i] - other._hash[i];
                }
            }
            return 0;
        }

        public readonly bool Equals(HashWrapper other)
        {
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                if (_hash[i] != other._hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        public readonly override int GetHashCode()
        {
            int hashcode = 0;
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                hashcode ^= _hash[i];
            }
            return hashcode;
        }

        public readonly override string ToString()
        {
            string str = string.Empty;
            fixed (int* values = _hash)
            {
                var bytes = new Span<byte>(values, HASH_SIZE_IN_BYTES);
                for (int i = 0; i < HASH_SIZE_IN_BYTES; i++)
                {
                    str += bytes[i].ToString("X2");
                }
            }
            return str;
        }
    }
}
