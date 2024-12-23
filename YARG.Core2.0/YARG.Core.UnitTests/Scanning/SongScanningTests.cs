using NUnit.Framework;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;

namespace YARG.Core.UnitTests.Scanning
{
    public class SongScanningTests
    {
        private List<string> songDirectories;
        private readonly bool MULTITHREADING = true;
        private readonly bool ALLOW_DUPLICATES = true;
        private readonly bool FULL_DIRECTORY_PATHS = false;
        private static readonly string SongCachePath = Path.Combine(Environment.CurrentDirectory, "songcache.bin");
        private static readonly string BadSongsPath = Path.Combine(Environment.CurrentDirectory, "badsongs.txt");

        [SetUp]
        public void Setup()
        {
            songDirectories = new()
            {
                
            };
            Assert.That(songDirectories, Is.Not.Empty, "Add directories to scan for the test");
        }

        [TestCase]
        public void FullScan()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());
            var cache = CacheHandler.RunScan(false, SongCachePath, BadSongsPath, MULTITHREADING, ALLOW_DUPLICATES, FULL_DIRECTORY_PATHS, songDirectories);
            // TODO: Any cache properties we want to check here?
            // Currently the only fail condition would be an unhandled exception
        }

        [TestCase]
        public void QuickScan()
        {
            YargLogger.AddLogListener(new DebugYargLogListener());
            var cache = CacheHandler.RunScan(true, SongCachePath, BadSongsPath, MULTITHREADING, ALLOW_DUPLICATES, FULL_DIRECTORY_PATHS, songDirectories);
            // TODO: see above
        }
    }
}
