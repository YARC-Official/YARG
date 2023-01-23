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
	}
}