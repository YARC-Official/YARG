using System.IO;

namespace YARG {
	public static class SongLibrary {
		public static readonly DirectoryInfo SONG_FOLDER = new(@"B:\Clone Hero Alpha\Songs");
		public static FileInfo CacheFile => new(Path.Combine(SONG_FOLDER.ToString(), "yarg_cache.json"));
	}
}