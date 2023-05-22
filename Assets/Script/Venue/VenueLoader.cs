using System.IO;
using UnityEngine;
using YARG.Song;

namespace YARG.Venue {
	public enum VenueType {
		Yarground,
		Video,
		Image
	}

	public static class VenueLoader {
		public readonly struct TypePathPair {
			public readonly VenueType Type;
			public readonly string Path;

			public TypePathPair(VenueType type, string path) {
				Type = type;
				Path = path;
			}
		}

		public static string VenueFolder => Path.Combine(GameManager.PersistentDataPath, "venue");

		static VenueLoader() {
			if (!Directory.Exists(VenueFolder)) {
				Directory.CreateDirectory(VenueFolder);
			}
		}

		public static TypePathPair? GetVenuePath(SongEntry song) {
			// Try a local yarground first

			string backgroundPath = Path.Combine(song.Location, "bg.yarground");
			if (File.Exists(backgroundPath)) {
				return new(VenueType.Yarground, backgroundPath);
			}

			// Then, a local picture or video

			string[] fileNames = {
				"bg",
				"background",
				"video"
			};

			string[] videoExtensions = {
				".mp4",
				".mov",
				".webm",
			};

			foreach (var name in fileNames) {
				foreach (var ext in videoExtensions) {
					var path = Path.Combine(song.Location, name + ext);

					if (File.Exists(path)) {
						return new(VenueType.Video, path);
					}
				}
			}

			string[] imageExtensions = {
				".png",
				".jpg",
				".jpeg",
			};

			foreach (var name in fileNames) {
				foreach (var ext in imageExtensions) {
					var path = Path.Combine(song.Location, name + ext);

					if (File.Exists(path)) {
						return new(VenueType.Image, path);
					}
				}
			}

			// If all of this fails, we can load a global venue
			return GetVenuePathFromGlobal();
		}

		private static TypePathPair? GetVenuePathFromGlobal() {
			var filePaths = Directory.GetFiles(VenueFolder);

			if (filePaths.Length == 0) {
				return null;
			}

			var path = filePaths[Random.Range(0, filePaths.Length)];
			var extension = Path.GetExtension(path);

			return extension switch {
				".yarground" => new(VenueType.Yarground, path),
				".mp4" or ".mov" or ".webm" => new(VenueType.Video, path),
				".png" or ".jpg" or ".jpeg" => new(VenueType.Image, path),
				_ => null,
			};
		}
	}
}