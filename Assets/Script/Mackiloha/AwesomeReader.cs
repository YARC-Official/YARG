using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

// Custom inherited class created with big endian / little endian in mind
// Added in custom features as required

namespace Mackiloha
{
    public class AwesomeReader : BinaryReader
    {
        private bool _big;

        /// <summary>
        /// Creates AwesomeReader stream
        /// </summary>
        /// <param name="input">Stream</param>
        public AwesomeReader(Stream input)
            : base(input)
        {
            _big = false;
        }

        /// <summary>
        /// Creates AwesomeReader stream and sets byte order
        /// </summary>
        /// <param name="input">Stream</param>
        /// <param name="bigEndian">Byte order</param>
        public AwesomeReader(Stream input, bool bigEndian)
            : base(input)
        {
            _big = bigEndian;
        }

        /// <summary>
        /// Reads 16-bit float (Half-Precision)
        /// </summary>
        /// <returns>Float</returns>
        // public override Half ReadHalf()
        // {
        //     byte[] data = this.ReadBytes(2);
        //     if (_big) Array.Reverse(data);
        //     return BitConverter.ToHalf(data, 0);
        // } 

        /// <summary>
        /// Reads 16-bit float (Half-Precision)
        /// </summary>
        /// <returns>Float</returns>
        // public float ReadHalfAsSingle()
        // {
        //     byte[] data = this.ReadBytes(2);
        //     if (_big) Array.Reverse(data);
        //     return (float)BitConverter.ToHalf(data, 0);
        // }

        /// <summary>
        /// Reads 32-bit float (Single-Precision)
        /// </summary>
        /// <returns>Float</returns>
        public override float ReadSingle()
        {
            byte[] data = this.ReadBytes(4);
            if (_big) Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }

        /// <summary>
        /// Reads 16-bit integer
        /// </summary>
        /// <returns>Integer</returns>
        public override short ReadInt16()
        {
            byte[] data = this.ReadBytes(2);
            if (_big) Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }

        /// <summary>
        /// Reads 16-bit unsigned integer
        /// </summary>
        /// <returns>Integer</returns>
        public override ushort ReadUInt16()
        {
            byte[] data = this.ReadBytes(2);
            if (_big) Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        /// <summary>
        /// Reads 24-bit integer
        /// </summary>
        /// <returns>Integer</returns>
        public int ReadInt24()
        {
            byte[] data = this.ReadBytes(3);
            if (_big) Array.Reverse(data);
            return (int)(data[0] << 0 | data[1] << 8 | data[2] << 16);
        }

        /// <summary>
        /// Reads 24-bit unsigned integer
        /// </summary>
        /// <returns>Integer</returns>
        public uint ReadUInt24()
        {
            byte[] data = this.ReadBytes(3);
            if (_big) Array.Reverse(data);
            return (uint)(data[0] << 0 | data[1] << 8 | data[2] << 16);
        }

        /// <summary>
        /// Reads 32-bit integer
        /// </summary>
        /// <returns>Integer</returns>
        public override int ReadInt32()
        {
            byte[] data = this.ReadBytes(4);
            if (_big) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        /// <summary>
        /// Reads 32-bit unsigned integer
        /// </summary>
        /// <returns>Integer</returns>
        public override uint ReadUInt32()
        {
            byte[] data = this.ReadBytes(4);
            if (_big) Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        /// <summary>
        /// Reads 64-bit integer
        /// </summary>
        /// <returns>Integer</returns>
        public override long ReadInt64()
        {
            byte[] data = this.ReadBytes(8);
            if (_big) Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        /// <summary>
        /// Reads 64-bit unsigned integer
        /// </summary>
        /// <returns>Integer</returns>
        public override ulong ReadUInt64()
        {
            byte[] data = this.ReadBytes(8);
            if (_big) Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        /// <summary>
        /// Reads null-terminated string
        /// </summary>
        /// <returns>String</returns>
        public string ReadNullString()
        {
            List<byte> s = new List<byte>();

            while (true)
            {
                byte chr = this.ReadByte();

                if (chr == 0x00) break;
                else s.Add(chr);
            }

            return Encoding.UTF8.GetString(s.ToArray());
        }

        /// <summary>
        /// Gets or sets read byte order
        /// </summary>
        public bool BigEndian { get { return _big; } set { _big = value; } }

        /// <summary>
        /// Reads string with 32-bit length preceding
        /// </summary>
        /// <returns></returns>
        public override string ReadString()
        {
            int length = this.ReadInt32();
            byte[] data = this.ReadBytes(length);

            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Reads string with given length
        /// </summary>
        /// <returns></returns>
        public string ReadStringWithLength(int length)
        {
            byte[] data = this.ReadBytes(length);
            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Finds next instance of given bytes
        /// </summary>
        /// <param name="needle"></param>
        /// <returns></returns>
        public long FindNext(byte[] needle)
        {
            long startingOffset = this.BaseStream.Position;
            byte[] haystack;

            while (this.BaseStream.Position <= (this.BaseStream.Length - needle.Length))
            {
                haystack = this.ReadBytes(needle.Length);
                if (TheSame(needle, haystack))
                {
                    this.BaseStream.Position -= needle.Length; // Goes back to first instance.
                    return this.BaseStream.Position - startingOffset;
                }
                else this.BaseStream.Position -= needle.Length - 1;
            }

            return -1;
        }

        /// <summary>
        /// Compares two byte arrays
        /// </summary>
        /// <param name="first">Byte array 1</param>
        /// <param name="second">Byte array 2</param>
        /// <returns>Equal?</returns>
        private bool TheSame(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++) if (second[i] != first[i]) return false;

            return true;
        }
    }
}