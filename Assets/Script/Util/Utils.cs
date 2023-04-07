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
				"gh1"                 => "Guitar Hero I",
				"gh2"                 => "Guitar Hero II",
				"gh2dlc"              => "Guitar Hero II DLC",
				"gh80s"               => "Guitar Hero Encore: Rocks the 80's",
				"gh3"                 => "Guitar Hero III: Legends of Rock",
				"ghot"                => "Guitar Hero On Tour",
				"gha"                 => "Guitar Hero: Aerosmith",
				"ghwt"                => "Guitar Hero World Tour",
				"ghm"                 => "Guitar Hero Metallica",
				"ghwor"               => "Guitar Hero: Warriors of Rock",
				"ghvh"                => "Guitar Hero: Van Halen",
				"gh2dx"               => "Guitar Hero II Deluxe",
				"gh2dxdlc"            => "Guitar Hero Rocks the 360",
				"gh80sdx"             => "Guitar Hero Encore Deluxe",
				"gh3dlc"              => "Guitar Hero III DLC",
				"ghwtdlc"             => "Guitar Hero World Tour DLC",
				"ghmdlc"              => "Death Magnetic DLC",
				"djhero"              => "DJ Hero",
				"ghsh"                => "Guitar Hero Smash Hits",
				"ghwordlc"            => "Guitar Hero Warriors of Rock DLC",
				"gh5"                 => "Guitar Hero 5",
				"gh5dlc"              => "Guitar Hero 5 DLC",
				"ghotd"               => "Guitar Hero On Tour: Decades",
				"ghotmh"              => "Guitar Hero On Tour: Modern Hits",
				"bandhero" or "bh"    => "Band Hero",
				"gh"                  => "Guitar Hero",
				"ghdlc"               => "Guitar Hero DLC",
				"ghl"                 => "Guitar Hero Live",
				"ghtv"                => "Guitar Hero TV",

				"rb1"                 => "Rock Band 1",
				"rb2"                 => "Rock Band 2",
				"rb3"                 => "Rock Band 3",
				"rb4"                 => "Rock Band 4",
				"tbrb" or "beatles"   => "The Beatles Rock Band",
				"tbrbdlc"             => "The Beatles: Rock Band DLC",
				"rbacdc"              => "Rock Band AC/DC",
				"gdrb"                => "Green Day Rock Band",
				"lrb"                 => "Lego Rock Band",
				"rbn"                 => "Rock Band Network",
				"ugc"                 => "Rock Band Network 1.0",
				"ugc_plus"            => "Rock Band Network 2.0",
				"ugc1"                => "Rock Band Network 1.0",
				"ugc2"                => "Rock Band Network 2.0",
				"ugc_lost"            => "Lost Rock Band Network",
				"rb1dlc"              => "Rock Band 1 DLC",
				"rb2dlc"              => "Rock Band 2 DLC",
				"rb3dlc"              => "Rock Band 3 DLC",
				"rb4dlc"              => "Rock Band 4 DLC",
				"rb4_dlc"             => "Rock Band 4 DLC",
				"rb4_rivals"          => "Rock Band Rivals",
				"rbtp_acdc"           => "Rock Band Track Pack: AC/DC Live",
				"rbtp_classic_rock"   => "Rock Band Track Pack: Classic Rock",
				"rbtp_country_1"      => "Rock Band Track Pack: Country 1",
				"rbtp_country_2"      => "Rock Band Track Pack: Country 2",
				"rbtp_metal"          => "Rock Band Track Pack: Metal",
				"rbtp_vol_1"          => "Rock Band Track Pack: Volume 1",
				"rbtp_vol_2"          => "Rock Band Track Pack: Volume 2",
				"rb_blitz"            => "Rock Band Blitz",
				"pearljam"            => "Pearl Jam: Rock Band",
				"greenday"            => "Green Day: Rock Band",
				"rbvr"                => "Rock Band VR",

				"comtpi"              => "Community Track Pack I",
				"comtpii"             => "Community Track Pack II",
				"comtpiii"            => "Community Track Pack III",
				"comtpiv"             => "Community Track Pack IV",
				"comtp45"             => "Community Track Pack 4.5",
				"comtpv"              => "Community Track Pack V",
				"plumato"             => "Plumato Charts",
				"chaotroperb2"        => "Chaotrope (RB2)",
				"maxaltitude"         => "Max Altitude",
				"finnish"             => "Suomibiisit",
				"bleepbloops"         => "Bleep Bloops",
				"solomedley"          => "Solo Medleys",
				"bleepbloopuc"        => "Bleep Bloop Undercharts",
				"customs"             => "Custom Songs",
				"ugc_c3"              => "C3 Customs",
				"c3customs"           => "C3 Customs",
				"c3legacy"            => "C3 Legacy",
				"milohax"             => "MiloHax Customs",
				"meme"                => "Meme Songs",
				"onyxite"             => "Onyxite Charts",
				"fof"                 => "Frets on Fire",
				"gd"                  => "GITADORA",
				"gf1"                 => "GuitarFreaks",
				"gf2dm1"              => "GuitarFreaks 2ndMIX & DrumMania",
				"praise"              => "Guitar Praise",
				"phase"               => "Phase Shift",
				"powergig"            => "PowerGig: Rise of the SixString",
				"rockrevolution"      => "Rock Revolution",
				"a2z"                 => "A-Z Pack",
				"ah1"                 => "Angevil Hero 1",
				"ah2"                 => "Angevil Hero 2",
				"antihero"            => "Anti Hero",
				"ahbe"                => "Anti Hero - Beach Episode",
				"antihero2"           => "Anti Hero 2",
				"aren"                => "ArenEternal's Charts",
				"bitcrusher"          => "BITCRUSHER",
				"bs"                  => "Blanket Statement",
				"cb"                  => "Circuit Breaker",
				"cth1r"               => "Carpal Tunnel Hero 1: Remastered",
				"cth2"                => "Carpal Tunnel Hero 2",
				"cth3"                => "Carpal Tunnel Hero 3",
				"charts"              => "CHARTS",
				"charts2"             => "CHARTS 2",
				"cowhero"             => "Cow Hero",
				"cowherodlc1"         => "Cow Hero DLC 1 - Bull Conqueror",
				"cowherodlc2"         => "Cow Hero DLC 2 - Bovine Champion",
				"cowherodlc3"         => "Cow Hero DLC 3 - Cattle Guardian",
				"creativech"          => "Creative Commons Hero",
				"csc"                 => "Custom Songs Central",
				"csc2018"             => "Custom Songs Central - 2018",
				"csc2019"             => "Custom Songs Central - 2019",
				"csc2020"             => "Custom Songs Central - 2020",
				"csc2021"             => "Custom Songs Central - 2021",
				"csc2022"             => "Custom Songs Central - 2022",
				"csc2023"             => "Custom Songs Central - 2023",
				"csc2024"             => "Custom Songs Central - 2024",
				"csc2025"             => "Custom Songs Central - 2025",
				"csc2026"             => "Custom Songs Central - 2026",
				"csc2027"             => "Custom Songs Central - 2027",
				"csc2028"             => "Custom Songs Central - 2028",
				"csc2029"             => "Custom Songs Central - 2029",
				"csc2030"             => "Custom Songs Central - 2030",
				"blitzrb2"            => "Rock Band Blitz (RB2)",
				"digi"                => "Digitizer",
				"facelift"            => "Facelift",
				"fp"                  => "Focal Point",
				"fp2"                 => "Focal Point 2",
				"ghxsetlist"          => "Guitar Hero X",
				"ghx2setlist"         => "Guitar Hero X-II",
				"guitarzero2"         => "Guitar Zero 2",
				"harmonyhero"         => "Harmony Hero",
				"helvian"             => "helvianalects",
				"justin"              => "Justin?'s Charts",
				"marathon"            => "Marathon Hero",
				"marathonhero2"       => "Marathon Hero 2",
				"paradigm"            => "Paradigm",
				"paramoremegapack"    => "Paramore Mega Pack",
				"a7xmegapack"         => "Avenged Sevenfold Mega Pack",
				"psh"                 => "Plastic Shred Hero",
				"ra"                  => "Redemption Arc",
				"tfoth"               => "The Fall of Troy Hero",
				"zgsb"                => "Zero Gravity - Space Battle",

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
