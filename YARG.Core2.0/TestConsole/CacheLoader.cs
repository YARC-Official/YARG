using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YARG.Core.Song;
using YARG.Core.Song.Cache;

namespace YARG.TestConsole
{
    public static class CacheLoader
    {
        private const bool MULTITHREADING = true;
        private const bool ALLOW_DUPLICATES = true;
        private const bool FULL_DIRECTORY_PATHS = false;

        private static readonly string SongDirsPath = Path.Combine(Environment.CurrentDirectory, "console_songdirs.txt");
        private static readonly string SongCachePath = Path.Combine(Environment.CurrentDirectory, "console_songcache.bin");
        private static readonly string BadSongsPath = Path.Combine(Environment.CurrentDirectory, "console_badsongs.txt");

        public static SongCache LoadCache()
        {
            if (!File.Exists(SongDirsPath))
                File.Create(SongDirsPath);

            var songDirectories = File.ReadAllLines(SongDirsPath)
                .Where(Directory.Exists)
                .ToList();
            if (songDirectories.Count < 1)
                throw new Exception($"Please add at least one song directory to {SongDirsPath}");

            var task = Task.Run(() => CacheHandler.RunScan(
                tryQuickScan: false,
                SongCachePath,
                BadSongsPath,
                MULTITHREADING,
                ALLOW_DUPLICATES,
                FULL_DIRECTORY_PATHS,
                songDirectories
            ));

            Console.WriteLine("Scanning songs:");
            var timer = Stopwatch.StartNew();
            int spinnerIndex = 0;
            char[] spinnerChars = ['/', '-', '\\', '|'];
            while (!task.IsCompleted)
            {
                var progress = CacheHandler.Progress;

                if (timer.ElapsedMilliseconds >= 125)
                {
                    timer.Restart();
                    spinnerIndex = (spinnerIndex + 1) % 4;
                }

                Console.Write($"({spinnerChars[spinnerIndex]}) {progress.Stage}: {progress.Count} songs");
                Thread.Sleep(10);

                string clear = new(' ', Console.CursorLeft);
                Console.CursorLeft = 0;
                Console.Write(clear);
                Console.CursorLeft = 0;
            }

            var cache = task.Result;
            Console.WriteLine($"Scanned {cache.Entries.Count} songs");

            return cache;
        }

        public static SongEntry LoadIni(SongCache cache, string directory)
        {
            Console.WriteLine($"Loading .ini song {directory}");
            foreach (var node in cache.Entries.Values)
            {
                foreach (var entry in node)
                {
                    if (entry is UnpackedIniEntry iniEntry && iniEntry.Location == directory)
                        return entry;
                }
            }

            throw new Exception($"Cannot find song {directory}");
        }

        public static SongEntry LoadSng(SongCache cache, string filePath)
        {
            Console.WriteLine($"Loading .sng song {filePath}");
            foreach (var node in cache.Entries.Values)
            {
                foreach (var entry in node)
                {
                    if (entry is SngEntry sngEntry && sngEntry.Location == filePath)
                        return entry;
                }
            }

            throw new Exception($"Cannot find song {filePath}");
        }

        public static SongEntry LoadCON(SongCache cache, string songId)
        {
            Console.WriteLine($"Loading CON song {songId}");
            foreach (var node in cache.Entries.Values)
            {
                foreach (var entry in node)
                {
                    if (entry is RBCONEntry conEntry && conEntry.RBSongId == songId)
                        return entry;
                }
            }

            throw new Exception($"Cannot find song {songId}");
        }
    }
}
