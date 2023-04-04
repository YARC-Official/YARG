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
				"gh1" or "gh1dlc"     => "Guitar Hero I",
				"gh2" or "gh2dlc"     => "Guitar Hero II",
				"gh80s"               => "Guitar Hero Encore: Rocks the 80's",
				"gh3" or "gh3dlc"     => "Guitar Hero III: Legends of Rock",
				"ghot"                => "Guitar Hero On Tour",
				"gha"                 => "Guitar Hero: Aerosmith",
				"ghwt" or "ghwtdlc"   => "Guitar Hero World Tour",
				"ghm"                 => "Guitar Hero Metallica",
				"ghwor" or "ghwordlc" => "Guitar Hero: Warriors of Rock",
				"ghvh"                => "Guitar Hero: Van Halen",
				
				"rb1" or "rb1dlc"     => "Rock Band 1",
				"rb2" or "rb2dlc"     => "Rock Band 2",
				"rb3" or "rb3dlc"     => "Rock Band 3",
				"rb4" or "rb4dlc"     => "Rock Band 4",
				"tbrb" or "tbrbdlc"   => "The Beatles Rock Band",
				"rbacdc"              => "Rock Band AC/DC",
				"gdrb"                => "Green Day Rock Band",
				"lrb"                 => "Lego Rock Band",
				"rbn"                 => "Rock Band Network",
				
				_                     => "Unknown/Custom"
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
		public static void CreateZipFromFiles(string outputZip, params string[] files) {
			using ZipArchive archive = ZipFile.Open(outputZip, ZipArchiveMode.Create);

			foreach (var path in files) {
				var file = new FileInfo(path);
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

		/// <param name="transform">The <see cref="RectTransform"/> to convert to screen space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the screen space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect RectTransformToScreenSpace(RectTransform transform) {
			// https://answers.unity.com/questions/1013011/convert-recttransform-rect-to-screen-space.html
			Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale.Abs());
			return new Rect((Vector2) transform.position - (size * transform.pivot), size);
		}

		/// <param name="transform">The <see cref="RectTransform"/> to convert to viewport space.</param>
		/// <returns>
		/// A <see cref="Rect"/> represting the viewport space of the specified <see cref="RectTransform"/>.
		/// </returns>
		public static Rect RectTransformToViewportSpace(RectTransform transform) {
			Rect rect = RectTransformToScreenSpace(transform);
			rect.width /= Screen.width;
			rect.height /= Screen.height;
			rect.x /= Screen.width;
			rect.y /= Screen.height;

			return rect;
		}

		/// <returns>
		/// The inputed note split into a note + octave.
		/// </returns>
		public static (float note, int octave) SplitNoteToOctaveAndNote(float note) {
			float outNote = note;
			int octave = 0;

			while (outNote > 12f) {
				octave++;
				outNote -= 12f;
			}

			return (outNote, octave);
		}

		/// <param name="v">The linear volume between 0 and 1.</param>
		/// <returns>
		/// The linear volume converted to decibels.
		/// </returns>
		public static float VolumeFromLinear(float v) {
			return Mathf.Log10(Mathf.Min(v + float.Epsilon, 1f)) * 20f;
		}
	}
}