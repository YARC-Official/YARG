using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace Mackiloha
{
    public class Crypt
    {
        public static void DecryptFile(string input, string output, bool newStyle, byte xor = 0x00)
        {
            int key;
            using var ms = new MemoryStream();

            // Gets key from input and copies remaining bytes to stream
            using (var fs = File.OpenRead(input))
            {
                byte[] keyBytes = new byte[4];
                fs.Read(keyBytes, 0, 4);
                key = BitConverter.ToInt32(keyBytes, 0);

                fs.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
            }

            // Decrypts stream
            DTBCrypt(ms, key, newStyle, xor);
            ms.Seek(0, SeekOrigin.Begin);

            // Writes stream to file
            File.WriteAllBytes(output, ms.ToArray());
        }

        public static void EncryptFile(string input, string output, bool newStyle, int key, byte xor = 0x00)
        {
            using var ms = new MemoryStream();

            // Writes key to stream
            ms.Write(BitConverter.GetBytes(key), 0, 4);

            // Copies input bytes to stream
            using (var fs = File.OpenRead(input))
            {
                fs.CopyTo(ms);
                ms.Seek(4, SeekOrigin.Begin);
            }

            // Encrypts stream
            DTBCrypt(ms, key, newStyle, xor);
            ms.Seek(0, SeekOrigin.Begin);

            File.WriteAllBytes(output, ms.ToArray());
        }

        /// <summary>
        /// Encrypts/decrypts input stream
        /// </summary>
        /// <param name="stream">Input</param>
        /// <param name="key">32-bit key</param>
        /// <param name="newStyle">PS2 = False | X360 = true</param>
        public static void DTBCrypt(Stream stream, int key, bool newStyle, byte xor = 0x00)
        {
            int b;
            long position = stream.Position;

            if (newStyle) // X360 version
            {
                // Crypts stream until it reaches file end.
                while ((b = stream.ReadByte()) > -1)
                {
                    key = X360_XOR(key);
                    stream.Seek(-1, SeekOrigin.Current);
                    stream.WriteByte((byte)(b ^ key ^ xor));
                }
                
                stream.Seek(position, SeekOrigin.Begin);
                return;
            }
            
            // PS2 version
            CryptTable table = new CryptTable(key);

            // Crypts stream until it reaches file end
            while ((b = stream.ReadByte()) > -1)
            {
                // PS2 - Code converted from ArkTool v6
                table.Table[table.Index1] ^= table.Table[table.Index2];
                stream.Seek(-1, SeekOrigin.Current);
                stream.WriteByte((byte)(b ^ table.Table[table.Index1] ^ xor));
                
                table.Index1 = ((table.Index1 + 1)) >= 0xF9 ? 0x00 : (table.Index1 + 1);
                table.Index2 = ((table.Index2 + 1)) >= 0xF9 ? 0x00 : (table.Index2 + 1);
            }

            // Goes back to starting position
            stream.Seek(position, SeekOrigin.Begin);
        }

        /// <summary>
        /// Used for X360 DTB encryption
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static int X360_XOR(int data)
        {
            int xorValue = ((data - ((data / 0x1F31D) * 0x1F31D)) * 0x41A7) - ((data / 0x1F31D) * 0xB14);
            return (xorValue <= 0) ? xorValue + 0x7FFFFFFF : xorValue;
        }

        private class CryptTable
        {
            /// <summary>
            /// Used for PS2 DTB encryption
            /// </summary>
            /// <param name="key"></param>
            public CryptTable(int key)
            {
                uint val1 = (uint)key;
                Table = new uint[0x100];
                Index1 = 0x00;
                Index2 = 0x67;

                for (int i = 0; i < Table.Length; i++)
                {
                    uint val2 = (val1 * 0x41C64E6D) + 0x3039;
                    val1 = (val2 * 0x41C64E6D) + 0x3039;
                    Table[i] = (val1 & 0x7FFF0000) | (val2 >> 16);
                }
            }

            public int Index1 { get; set; }
            public int Index2 { get; set; }
            public uint[] Table { get; set; }
        }

        public static string SHA1Hash(Stream stream)
        {
            using (SHA1Managed sha = new SHA1Managed())
            {
                byte[] hash = sha.ComputeHash(stream);
                return string.Join("", hash.Select(b => b.ToString("X2")).ToArray());
            }
        }
    }
}
