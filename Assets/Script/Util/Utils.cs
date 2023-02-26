using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace YARG.Util {
	public static class Utils {
		/// <returns>
		/// A unique hash for <paramref name="a"/>.
		/// </returns>
		public static string Hash(string a) {
			return Hash128.Compute(a).ToString();
		}

		/// <returns>
		/// The converted short name (gh1) into the game name (Guitar Hero 1).
		/// </returns>
		public static string SourceToGameName(string source) {
#pragma warning disable format
			return source switch {
				"gh1" or "gh1dlc"   => "Guitar Hero 1",
				"gh2" or "gh2dlc"   => "Guitar Hero 2",
				"ghm"               => "Guitar Hero Metallica",
				"ghwt"              => "Guitar Hero World Tour",
				
				"rb1" or "rb1dlc"   => "Rock Band 1",
				"rb2" or "rb2dlc"   => "Rock Band 2",
				"rb3" or "rb3dlc"   => "Rock Band 3",
				"tbrb" or "tbrbdlc" => "The Beatles Rock Band",
				"rbacdc"            => "Rock Band AC/DC",
				"gdrb"              => "Green Day Rock Band",
				"lrb"               => "Lego Rock Band",
				
				_                   => "Unknown/Custom"
			};
#pragma warning restore format
		}

		/// <summary>
		/// Read a file from a <see cref="NetworkStream"/>.
		/// </summary>
		public static void ReadFile(NetworkStream stream, FileInfo output) {
			const int BUF_SIZE = 81920;

			// Wait until data is available
			while (!stream.DataAvailable) {
				Thread.Sleep(100);
			}

			// Get file size
			var buffer = new byte[sizeof(long)];
			stream.Read(buffer, 0, sizeof(long));
			long size = BitConverter.ToInt64(buffer);

			// If the size is zero, the file did not exist on server
			if (size <= 0) {
				return;
			}

			// Copy data to disk
			// We can't use CopyTo on a infinite stream (like NetworkStream)
			long totalRead = 0;
			var fileBuf = new byte[BUF_SIZE];
			output.Delete();
			using var fs = output.OpenWrite();
			while (totalRead < size) {
				int bytesRead = stream.Read(fileBuf, 0, BUF_SIZE);
				fs.Write(fileBuf, 0, bytesRead);
				totalRead += bytesRead;
			}
		}

		/// <summary>
		/// Create a zip file from the specified <paramref name="files"/>.
		/// </summary>
		public static void CreateZipFromFiles(string outputZip, params FileInfo[] files) {
			using ZipArchive archive = ZipFile.Open(outputZip, ZipArchiveMode.Create);

			foreach (var file in files) {
				archive.CreateEntryFromFile(file.FullName, file.Name);
			}
		}

		/// <summary>
		/// Checks if the path <paramref name="a"/> is equal to the path <paramref name="b"/>.<br/>
		/// Paths are NOT case sensitive.
		/// </summary>
		public static bool ArePathsEqual(string a, string b) {
			return a.ToUpperInvariant() == b.ToUpperInvariant();
		}
	}
}