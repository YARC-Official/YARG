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

		public byte Game { get; set; }
		public byte BitsPerPixel { get; set; }
		public byte MipMaps { get; set; }

		public int Format { get; set; }

		public short Width { get; set; }
		public short Height { get; set; }
		public short BytesPerLine { get; set; }

		[JsonIgnore]
		private Texture2D _textureCache;

		public XboxImage(string path) {
			ImagePath = path;
		}

		public void ParseImageHeader() {
			using var fs = new FileStream(ImagePath, FileMode.Open, FileAccess.Read);
			using var br = new BinaryReader(fs, new ASCIIEncoding());

			// Parse header
			byte[] header = br.ReadBytes(32);
			Game = header[0];
			BitsPerPixel = header[1];
			Format = BitConverter.ToInt32(header, 2);
			MipMaps = header[6];
			Width = BitConverter.ToInt16(header, 7);
			Height = BitConverter.ToInt16(header, 9);
			BytesPerLine = BitConverter.ToInt16(header, 11);

			// // parse DXT-compressed blocks
			// byte[] DXTBlocks = br.ReadBytes((int)(fs.Length - 32));
		}

		/// <returns>
		/// A byte array of DXT1 formatted blocks to make into a Unity Texture
		/// </returns>
		public byte[] GetDXTBlocksFromImage() {
			using var fs = new FileStream(ImagePath, FileMode.Open, FileAccess.Read);
			using var br = new BinaryReader(fs, new ASCIIEncoding());

			// Skip header
			byte[] header = br.ReadBytes(32);
			byte[] DXTBlocks;

			// Parse DXT-compressed blocks, depending on format
			if ((BitsPerPixel == 0x04) && (Format == 0x08)) {
				// If DXT-1 format already, read the bytes straight up
				DXTBlocks = br.ReadBytes((int) (fs.Length - 32));
			} else {
				// If DXT-3 format, we have to omit the alpha bytes
				byte[] buf = new byte[8];
				List<byte> yuh = new List<byte>();
				buf = br.ReadBytes(8); //skip the first 8 alpha bytes
				for (int i = 8; i < (fs.Length - 32) / 2; i += 8) {
					buf = br.ReadBytes(8); // We want to read these 8 bytes
					Debug.Log($"reading {BitConverter.ToString(buf)}");
					yuh.AddRange(buf);
					buf = br.ReadBytes(8); // and skip these 8 bytes
					Debug.Log($"skipping {BitConverter.ToString(buf)}");
				}
				DXTBlocks = yuh.ToArray();
			}

			// Swap bytes because xbox is weird like that
			Parallel.For(0, DXTBlocks.Length / 2, i => {
				(DXTBlocks[i * 2], DXTBlocks[i * 2 + 1]) = (DXTBlocks[i * 2 + 1], DXTBlocks[i * 2]);
			});

			return DXTBlocks;
		}

		public Texture2D GetAsTexture() {
			if (_textureCache != null) {
				return _textureCache;
			}

			// Load texture
			_textureCache = new Texture2D(Width, Height, GraphicsFormat.RGBA_DXT1_SRGB, TextureCreationFlags.None);
			_textureCache.LoadRawTextureData(GetDXTBlocksFromImage());
			_textureCache.Apply();

			return _textureCache;
		}
	}
}