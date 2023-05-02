using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace YARG.Serialization {
	[JsonObject(MemberSerialization.OptOut)]
	public class XboxImage {
		public string ImagePath { get; set; }
		public byte BitsPerPixel { get; set; }
		public int Format { get; set; }
		public short Width { get; set; }
		public short Height { get; set; }
		private uint ImgSize;
        private uint[] ImgOffsets;
		private bool isFromCON = false;

		[JsonIgnore]
		private Texture2D _textureCache;

		public XboxImage(string path) {
			ImagePath = path;
		}

		public XboxImage(string path, uint size, uint[] offsets){
			ImagePath = path;
			ImgSize = size;
			ImgOffsets = offsets;
			isFromCON = true;
		}

		/// <returns>
		/// A byte array of DXT1 formatted blocks to make into a Unity Texture
		/// </returns>
		public byte[] GetDXTBlocksFromImage() {
			if(!isFromCON){ //raw
				using var fs = new FileStream(ImagePath, FileMode.Open, FileAccess.Read);
				using var br = new BinaryReader(fs, new ASCIIEncoding());

				// Parse header
				byte[] header = br.ReadBytes(32);
				BitsPerPixel = header[1];
				Format = BitConverter.ToInt32(header, 2);
				Width = BitConverter.ToInt16(header, 7);
				Height = BitConverter.ToInt16(header, 9);
				byte[] DXTBlocks;

				// Parse DXT-compressed blocks, depending on format
				if ((BitsPerPixel == 0x04) && (Format == 0x08)) {
					// If DXT-1 format already, read the bytes straight up
					fs.Seek(32, SeekOrigin.Begin);
					DXTBlocks = br.ReadBytes((int) (fs.Length - 32));
				} else {
					// If DXT-3 format, we have to omit the alpha bytes
					List<byte> extractedDXT3 = new List<byte>();
					br.ReadBytes(8); //skip the first 8 alpha bytes
					for (int i = 8; i < (fs.Length - 32) / 2; i += 8) {
						extractedDXT3.AddRange(br.ReadBytes(8)); // We want to read these 8 bytes
						br.ReadBytes(8); // and skip these 8 bytes
					}
					DXTBlocks = extractedDXT3.ToArray();
				}

				// Swap bytes because xbox is weird like that
				Parallel.For(0, DXTBlocks.Length / 2, i => {
					(DXTBlocks[i * 2], DXTBlocks[i * 2 + 1]) = (DXTBlocks[i * 2 + 1], DXTBlocks[i * 2]);
				});

				return DXTBlocks;
			}
			else{ //CON
				byte[] f = new byte[ImgSize];
				uint lastSize = ImgSize % 0x1000;

				Parallel.For(0, ImgOffsets.Length, i => {
					uint readLen = (i == ImgOffsets.Length - 1) ? lastSize : 0x1000;
					using var fs = new FileStream(ImagePath, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs, new ASCIIEncoding());
					fs.Seek(ImgOffsets[i], SeekOrigin.Begin);
					Array.Copy(br.ReadBytes((int)readLen), 0, f, i*0x1000, (int)readLen);
				});

				MemoryStream ms = new MemoryStream(f);

				// Parse header
				byte[] header = ms.ReadBytes(32);
				BitsPerPixel = header[1];
				Format = BitConverter.ToInt32(header, 2);
				Width = BitConverter.ToInt16(header, 7);
				Height = BitConverter.ToInt16(header, 9);
				byte[] DXTBlocks;

				// Parse DXT-compressed blocks, depending on format
				if ((BitsPerPixel == 0x04) && (Format == 0x08)) {
					// If DXT-1 format already, read the bytes straight up
					DXTBlocks = ms.ReadBytes((int) (ImgSize - 32));
				}
				else{
					// If DXT-3 format, we have to omit the alpha bytes
					List<byte> extractedDXT3 = new List<byte>();
					ms.ReadBytes(8); //skip the first 8 alpha bytes
					for (int i = 8; i < (ImgSize - 32) / 2; i += 8) {
						extractedDXT3.AddRange(ms.ReadBytes(8)); // We want to read these 8 bytes
						ms.ReadBytes(8); // and skip these 8 bytes
					}
					DXTBlocks = extractedDXT3.ToArray();
				}

				// Swap bytes because xbox is weird like that
				Parallel.For(0, DXTBlocks.Length / 2, i => {
					(DXTBlocks[i * 2], DXTBlocks[i * 2 + 1]) = (DXTBlocks[i * 2 + 1], DXTBlocks[i * 2]);
				});

				return DXTBlocks;
			}
		}

		public Texture2D GetAsTexture() {
			if (_textureCache != null) {
				return _textureCache;
			}
			// parse image for DXT blocks (and width and height from header)
			byte[] data = GetDXTBlocksFromImage();

			// Load texture
			_textureCache = new Texture2D(Width, Height, GraphicsFormat.RGBA_DXT1_SRGB, TextureCreationFlags.None);
			_textureCache.LoadRawTextureData(data);
			_textureCache.Apply();

			return _textureCache;
		}
	}
}