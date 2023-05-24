using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

// The below code was originally written by Arkem in Python, on this repo: https://github.com/arkem/py360
// For the purposes of YARG, the relevant code has been ported to C#,
// so that YARG can read the contents of a Rock Band CON file (.mid, .mogg, .dta, .png_xbox, etc)

namespace XboxSTFS {

	public class ConMissingException : Exception {
		public ConMissingException() : base("Original con file not found") { }
	}
    // Object containing the information about a file in the STFS container
    public class FileListing {
        public string Filename { get; private set; }
        public byte Flags { get; private set; }
        public int NumBlocks { get; private set; }
        public int FirstBlock { get; private set; }
		public short PathIndex { get; private set; }
		public int Size { get; private set; }
		public int LastWrite { get; private set; }
        

        public FileListing(ReadOnlySpan<byte> data){
            Filename = System.Text.Encoding.UTF8.GetString(data.ToArray(), 0, 0x28).TrimEnd('\0');
            Flags = data[0x28];
            
            NumBlocks = BitConverter.ToInt32(new byte[4] { data[0x29], data[0x2A], data[0x2B], 0x00 });
            FirstBlock = BitConverter.ToInt32(new byte[4] { data[0x2F], data[0x30], data[0x31], 0x00 });
            PathIndex = BitConverter.ToInt16(new byte[2] { data[0x33], data[0x32] });
            Size = BitConverter.ToInt32(new byte[4] { data[0x37], data[0x36], data[0x35], data[0x34] });
			LastWrite = BitConverter.ToInt32(new byte[4] { data[0x3B], data[0x3A], data[0x39], data[0x38] });
		}

		public void SetParentDirectory(string parentDirectory) {
			Filename = Path.Combine(parentDirectory, Filename);
		}

        public FileListing(){}

        public override string ToString() => $"STFS File Listing: {Filename}";
        public bool IsDirectory() { return (Flags & 0x80) > 0; }
        public bool IsContiguous() { return (Flags & 0x40) > 0; }
    }

	public class XboxSTFSFile {
		public string Filename { get; private set; }
		private FileStream stream;
		private List<FileListing> files = new();
		private byte shiftValue;

		public XboxSTFSFile() {}

		public bool Load(string filename) {
			byte[] buffer = new byte[4];
			stream = new FileStream(filename, FileMode.Open, FileAccess.Read);

			if (stream.Read(buffer) != 4)
				return false;

			string tag = Encoding.Default.GetString(buffer, 0, buffer.Length);
			if (tag != "CON " && tag != "LIVE" && tag != "PIRS")
				return false;

			stream.Seek(0x0340, SeekOrigin.Begin);
			if (stream.Read(buffer) != 4)
				return false;

			if (((((buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]) + 0xFFF) & 0xF000) >> 0xC) != 0xB)
				shiftValue = 1;

			stream.Seek(0x37C, SeekOrigin.Begin);
			if (stream.Read(buffer, 0, 2) != 2)
				return false;

			int length = 0x1000 * (buffer[0] << 8 | buffer[1]);

			stream.Seek(0x37E, SeekOrigin.Begin);
			if (stream.Read(buffer, 0, 3) != 3)
				return false;

			int firstBlock = buffer[0] << 16 | buffer[1] << 8 | buffer[2];

			byte[] fileListingBuffer = ReadContiguousBlocks(firstBlock, length);
			for (int i = 0; i < length; i += 0x40) {
				FileListing listing = new FileListing(new ReadOnlySpan<byte>(fileListingBuffer, i, 0x40));
				if (listing.Filename.Length == 0)
					break;

				if (listing.PathIndex != -1)
					listing.SetParentDirectory(files[listing.PathIndex].Filename);
				files.Add(listing);
			}

			Filename = filename;
			return true;
		}

		public int GetFileIndex(string filename)
		{
			for (int i = 0; i < files.Count; ++i)
				if (filename == files[i].Filename)
					return i;
			return -1;
		}

		public FileListing this[int index] { get { return files[index]; } }

		public byte[] LoadSubFile(string filename)
		{
			for (int i = 0; i < files.Count; ++i)
				if (filename == files[i].Filename)
					return LoadSubFile(files[i]);
			Debug.Log("File " + filename + " does not exist in CON and thus could not be loaded");
			return new byte[0];
		}

		public byte[] LoadSubFile(int index)
		{
			if (index < 0 || index >= files.Count)
				return new byte[0];
			return LoadSubFile(files[index]);
		}

		public byte[] LoadSubFile(FileListing listing)
		{
			Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
			if (listing.IsContiguous())
				return ReadContiguousBlocks(listing.FirstBlock, listing.Size);
			else
				return ReadSplitBlocks(listing.FirstBlock, listing.Size);
		}

		public bool IsMoggUnencrypted(int index) {
			if (index < 0 || index >= files.Count)
				throw new Exception("Index provided is not valid");

			var listing = files[index];
			Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");

			long blockLocation = 0xC000 + (long)CalculateBlockNum(listing.FirstBlock) * 0x1000;
			if (stream.Seek(blockLocation, SeekOrigin.Begin) != blockLocation)
				throw new Exception("Seek error in CON-like subfile for Mogg");
			return stream.ReadInt32LE() == 0xA;
		}

		private int CalculateBlockNum(int blocknum)
		{
			int blockAdjust = 0;
			if (blocknum >= 0xAA)
			{
				blockAdjust += ((blocknum / 0xAA) + 1) << shiftValue;
				if (blocknum >= 0x70E4)
					blockAdjust += ((blocknum / 0x70E4) + 1) << shiftValue;
			}
			return blockAdjust + blocknum;
		}

		private byte[] ReadContiguousBlocks(int blockNum, int fileSize)
		{
			byte[] data = new byte[fileSize];
			{
				long pos = 0xC000 + (long)CalculateBlockNum(blockNum) * 0x1000;
				if (stream.Seek(pos, SeekOrigin.Begin) != pos)
					throw new Exception("File location is not valid");
			}
			
			long skipVal = 0x1000 << shiftValue;
			int div = blockNum / 28900;
			int numBlocks = 170 - (blockNum % 170);
			int readSize = 0x1000 * numBlocks;
			int offset = 0;
			while (true)
			{
				if (readSize > fileSize - offset)
					readSize = fileSize - offset;

				if (stream.Read(data, offset, readSize) != readSize)
					throw new Exception("Read error in CON-like subfile - Type: Contiguous");

				offset += readSize;
				if (offset == fileSize)
					break;

				blockNum += numBlocks;
				numBlocks = 170;
				readSize = 170 * 0x1000;

				stream.Seek(skipVal, SeekOrigin.Current);
				if (blockNum == 170)
					stream.Seek(skipVal, SeekOrigin.Current);
				else if (blockNum == (div + 1) * 28900)
				{
					if (blockNum == 28900)
						stream.Seek(skipVal, SeekOrigin.Current);
					stream.Seek(skipVal, SeekOrigin.Current);
					++div;
				}
			}
			return data;
		}

		byte[] ReadSplitBlocks(int blockNum, int fileSize)
		{
			byte[] data = new byte[fileSize];
			byte[] buffer = new byte[3];

			int offset = 0;
			while (true)
			{
				int block = CalculateBlockNum(blockNum);
				long blockLocation = 0xC000 + (long)block * 0x1000;
				if (stream.Seek(blockLocation, SeekOrigin.Begin) != blockLocation)
					throw new Exception("Seek error in CON-like subfile - Type: Split");

				int readSize = 0x1000;
				if (readSize > fileSize - offset)
					readSize = fileSize - offset;

				if (stream.Read(data, offset, readSize) != readSize)
					throw new Exception("Read error in CON-like subfile - Type: Split");

				offset += readSize;
				if (offset == fileSize)
					break;

				long hashlocation = blockLocation - ((blockNum % 170) * 4072 + 4075);
				if (stream.Seek(hashlocation, SeekOrigin.Begin) != hashlocation)
					throw new Exception("Seek error in CON-like subfile - Type: Split");

				if (stream.Read(buffer, 0, 3) != 3)
					throw new Exception("Read error in CON-like subfile - Type: Split");

				blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
			}
			return data;
		}
	}
}