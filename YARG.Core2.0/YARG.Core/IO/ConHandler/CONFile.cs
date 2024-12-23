using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public static class CONFile
    {
        private static readonly FourCC CON_TAG = new('C', 'O', 'N', ' ');
        private static readonly FourCC LIVE_TAG = new('L', 'I', 'V', 'E');
        private static readonly FourCC PIRS_TAG = new('P', 'I', 'R', 'S');

        private const int METADATA_POSITION = 0x340;
        private const int FILETABLEBLOCKCOUNT_POSITION = 0x37C;
        private const int FILETABLEFIRSTBLOCK_POSITION = 0x37E;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;

        private const int BYTES_PER_BLOCK = 0x1000;
        private const int SIZEOF_FILELISTING = 0x40;

        public static bool TryGetListing(this List<CONFileListing> listings, string name, out CONFileListing listing)
        {
            foreach (var file in listings)
            {
                if (file.Filename == name)
                {
                    listing = file;
                    return true;
                }
            }
            listing = null!;
            return false;
        }

        public static List<CONFileListing>? TryParseListings(in AbridgedFileInfo info, FileStream filestream)
        {
            Span<byte> int32Buffer = stackalloc byte[BYTES_32BIT];
            if (filestream.Read(int32Buffer) != BYTES_32BIT)
                return null;

            var tag = new FourCC(int32Buffer);
            if (tag != CON_TAG && tag != LIVE_TAG && tag != PIRS_TAG)
                return null;

            filestream.Seek(METADATA_POSITION, SeekOrigin.Begin);
            if (filestream.Read(int32Buffer) != BYTES_32BIT)
                return null;

            byte shift = 0;
            int entryID = int32Buffer[0] << 24 | int32Buffer[1] << 16 | int32Buffer[2] << 8 | int32Buffer[3];

            // Docs: "If bit 12, 13 and 15 of the Entry ID are on, there are 2 hash tables every 0xAA (170) blocks"
            if ((entryID + 0xFFF & 0xF000) >> 0xC != 0xB)
                shift = 1;

            filestream.Seek(FILETABLEBLOCKCOUNT_POSITION, SeekOrigin.Begin);
            if (filestream.Read(int32Buffer[..BYTES_16BIT]) != BYTES_16BIT)
                return null;

            int length = BYTES_PER_BLOCK * (int32Buffer[0] | int32Buffer[1] << 8);

            filestream.Seek(FILETABLEFIRSTBLOCK_POSITION, SeekOrigin.Begin);
            if (filestream.Read(int32Buffer[..BYTES_24BIT]) != BYTES_24BIT)
                return null;

            int firstBlock = int32Buffer[0] << 16 | int32Buffer[1] << 8 | int32Buffer[2];
            try
            {
                var listings = new List<CONFileListing>();

                using var listingBuffer = CONFileStream.LoadFile(filestream, true, length, firstBlock, shift);
                unsafe
                {
                    var endPtr = listingBuffer.Ptr + length;
                    for (var currPtr = listingBuffer.Ptr; currPtr + SIZEOF_FILELISTING <= endPtr && currPtr[0] != 0; currPtr += SIZEOF_FILELISTING)
                    {
                        short pathIndex = (short) (currPtr[0x32] << 8 | currPtr[0x33]);
                        if (pathIndex >= listings.Count)
                        {
                            YargLogger.LogFormatError("Error while parsing {0} - Filelisting blocks constructed out of spec", info.FullName);
                            return null;
                        }

                        string filename = pathIndex >= 0 ? listings[pathIndex].Filename + "/" : string.Empty;
                        filename += Encoding.UTF8.GetString(currPtr, 0x28).TrimEnd('\0');
                        listings.Add(new CONFileListing(info, filename, pathIndex, shift, currPtr));
                    }
                }
                return listings;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while parsing {info.FullName}");
                return null;
            }
        }
    }
}
