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
			Filename = Encoding.UTF8.GetString(data.ToArray(), 0, 0x28).TrimEnd('\0');
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
		public string Filename { get { return stream.Name; } }
		private FileStream stream;
		private byte shift = 0;
		private List<FileListing> files = new();
		private readonly object fileLock = new();

		static public XboxSTFSFile LoadCON(string filename) {
			byte[] buffer = new byte[4];
			FileStream stream = new(filename, FileMode.Open, FileAccess.Read);

			if (stream.Read(buffer) != 4)
				return null;

			string tag = Encoding.Default.GetString(buffer, 0, buffer.Length);
			if (tag != "CON " && tag != "LIVE" && tag != "PIRS")
				return null;

			stream.Seek(0x0340, SeekOrigin.Begin);
			if (stream.Read(buffer) != 4)
				return null;

			byte shift = 0;
			int entryID = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];
			if ((((entryID + 0xFFF) & 0xF000) >> 0xC) != 0xB)
				shift = 1;

			stream.Seek(0x37C, SeekOrigin.Begin);
			if (stream.Read(buffer, 0, 2) != 2)
				return null;

			int length = 0x1000 * (buffer[0] << 8 | buffer[1]);

			stream.Seek(0x37E, SeekOrigin.Begin);
			if (stream.Read(buffer, 0, 3) != 3)
				return null;

			int firstBlock = buffer[0] << 16 | buffer[1] << 8 | buffer[2];

			XboxSTFSFile con = new(stream, shift);
			try {
				con.ParseFileList(firstBlock, length);
				return con;
			} catch(Exception) { return null; }
		}

		private XboxSTFSFile(FileStream stream, byte shift) {
			this.stream = stream;
			this.shift = shift;
		}

		private void ParseFileList(int firstBlock, int length) {
			byte[] fileListingBuffer = ReadContiguousBlocks(firstBlock, length);
			for (int i = 0; i < length; i += 0x40) {
				FileListing listing = new(new ReadOnlySpan<byte>(fileListingBuffer, i, 0x40));
				if (listing.Filename.Length == 0)
					break;

				if (listing.PathIndex != -1)
					listing.SetParentDirectory(files[listing.PathIndex].Filename);
				files.Add(listing);
			}
		}

		public int GetFileIndex(string filename)
		{
			for (int i = 0; i < files.Count; ++i)
				if (filename == files[i].Filename)
					return i;
			return -1;
		}

		public FileListing this[int index] { get { return files[index]; } }

		public byte[] LoadSubFile(string filename) {
			for (int i = 0; i < files.Count; ++i)
				if (filename == files[i].Filename) {
					lock (fileLock) {
						return Load(files[i]);
					}
				}
			Debug.Log("File " + filename + " does not exist in CON and thus could not be loaded");
			return new byte[0];
		}

		public byte[] LoadSubFile(int index) {
			if (index < 0 || index >= files.Count)
				return Array.Empty<byte>();

			lock (fileLock) {
				return Load(files[index]);
			}
		}

		public bool IsMoggUnencrypted(int index) {
			if (index < 0 || index >= files.Count)
				throw new Exception("Index provided is not valid");

			var listing = files[index];
			Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");

			long blockLocation = 0xC000 + (long)CalculateBlockNum(listing.FirstBlock) * 0x1000;

			lock (fileLock) {
				if (stream.Seek(blockLocation, SeekOrigin.Begin) != blockLocation)
					throw new Exception("Seek error in CON-like subfile for Mogg");
				return stream.ReadInt32LE() == 0xA;
			}
		}

		private byte[] Load(FileListing listing) {
			Debug.Assert(!listing.IsDirectory(), "Directory listing cannot be loaded as a file");
			try {
				if (listing.IsContiguous())
					return ReadContiguousBlocks(listing.FirstBlock, listing.Size);
				else
					return ReadSplitBlocks(listing.FirstBlock, listing.Size);
			} catch (Exception e) {
				throw new Exception(Filename + ": " + e.Message);
			}
		}

		internal const int BYTES_PER_BLOCK = 0x1000;
		internal const int BLOCKS_PER_SECTION = 170;
		internal const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
		internal const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

		private byte[] ReadContiguousBlocks(int blockNum, int fileSize) {
			byte[] data = new byte[fileSize];
			{
				long pos = 0xC000 + (long)CalculateBlockNum(blockNum) * BYTES_PER_BLOCK;
				if (stream.Seek(pos, SeekOrigin.Begin) != pos)
					throw new Exception("File location is not valid");
			}

			long skipVal = BYTES_PER_BLOCK << shift;
			int threshold = blockNum - (blockNum % NUM_BLOCKS_SQUARED) + NUM_BLOCKS_SQUARED;
			int numBlocks = BLOCKS_PER_SECTION - (blockNum % BLOCKS_PER_SECTION);
			int readSize = BYTES_PER_BLOCK * numBlocks;
			int offset = 0;
			while (true) {
				if (readSize > fileSize - offset)
					readSize = fileSize - offset;

				if (stream.Read(data, offset, readSize) != readSize)
					throw new Exception("Read error in CON-like subfile - Type: Contiguous");

				offset += readSize;
				if (offset == fileSize)
					break;

				blockNum += numBlocks;
				numBlocks = BLOCKS_PER_SECTION;
				readSize = BYTES_PER_SECTION;

				int seekCount = 1;
				if (blockNum == BLOCKS_PER_SECTION)
					seekCount = 2;
				else if (blockNum == threshold) {
					if (blockNum == NUM_BLOCKS_SQUARED)
						seekCount = 2;
					++seekCount;
					threshold += NUM_BLOCKS_SQUARED;
				}

				stream.Seek(seekCount * skipVal, SeekOrigin.Current);
			}
			return data;
		}

		private byte[] ReadSplitBlocks(int blockNum, int fileSize) {
			byte[] data = new byte[fileSize];
			byte[] buffer = new byte[3];

			int offset = 0;
			while (true) {
				int block = CalculateBlockNum(blockNum);
				long blockLocation = 0xC000 + (long)block * BYTES_PER_BLOCK;

				if (stream.Seek(blockLocation, SeekOrigin.Begin) != blockLocation)
					throw new Exception("Pre-Seek error in CON-like subfile - Type: Split");

				int readSize = BYTES_PER_BLOCK;
				if (readSize > fileSize - offset)
					readSize = fileSize - offset;

				if (stream.Read(data, offset, readSize) != readSize)
					throw new Exception("Pre-Read error in CON-like subfile - Type: Split");

				offset += readSize;
				if (offset == fileSize)
					break;

				long hashlocation = blockLocation - ((long)(blockNum % BLOCKS_PER_SECTION) * 4072 + 4075);
				if (stream.Seek(hashlocation, SeekOrigin.Begin) != hashlocation)
					throw new Exception("Post-Seek error in CON-like subfile - Type: Split");

				if (stream.Read(buffer, 0, 3) != 3)
					throw new Exception("Post-Read error in CON-like subfile - Type: Split");

				blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
			}
			return data;
		}

		private int CalculateBlockNum(int blocknum) {
			int blockAdjust = 0;
			if (blocknum >= BLOCKS_PER_SECTION) {
				blockAdjust += ((blocknum / BLOCKS_PER_SECTION) + 1) << shift;
				if (blocknum >= NUM_BLOCKS_SQUARED)
					blockAdjust += ((blocknum / NUM_BLOCKS_SQUARED) + 1) << shift;
			}
			return blockAdjust + blocknum;
		}
	}
}
