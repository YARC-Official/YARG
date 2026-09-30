using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.Persistent;

namespace YARG.Song
{
    public static class DemucsStemGenerator
    {
        private const long MinimumMixBytes = 4096;

        private static readonly object Gate = new();
        private static bool _running;
        private static int _launcher = -1;

        public static bool IsRunning
        {
            get
            {
                lock (Gate)
                {
                    return _running;
                }
            }
        }

        public static void Start(SongEntry song, Action onFinished)
        {
            lock (Gate)
            {
                if (_running)
                {
                    ToastManager.ToastInformation(Localize.Key("Menu.Toast.StemsBusy"));
                    return;
                }

                if (song.GetStemSeparation() == StemSeparation.Present)
                {
                    ToastManager.ToastInformation(Localize.Key("Menu.Toast.StemsAlreadyPresent"));
                    return;
                }

                _running = true;
            }

            ToastManager.ToastInformation(Localize.KeyFormat("Menu.Toast.StemsGenerating", song.Name.ToString()));
            Task.Run(() =>
            {
                string error = null;
                var succeeded = false;
                try
                {
                    succeeded = Execute(song, out error);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    YargLogger.LogException(ex, "Demucs stem generation failed");
                }
                finally
                {
                    var finished = onFinished;
                    var message = error;
                    var ok = succeeded;
                    if (!ok)
                    {
                        YargLogger.LogError("Demucs stem generation failed: " + (message ?? "Unknown error"));
                        message = SummarizeError(message);
                    }
                    UnityMainThreadCallback.QueueEvent(() =>
                    {
                        lock (Gate)
                        {
                            _running = false;
                        }

                        if (ok)
                        {
                            song.InvalidateStemSeparation();
                            ToastManager.ToastSuccess(Localize.Key("Menu.Toast.StemsGenerated"));
                            finished?.Invoke();
                        }
                        else
                        {
                            ToastManager.ToastError(Localize.KeyFormat("Menu.Toast.StemsFailed", message ?? "Unknown error"));
                        }
                    });
                }
            });
        }

        private static bool Execute(SongEntry song, out string error)
        {
            string temp = Path.Combine(Path.GetTempPath(), "yarg-demucs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                if (!song.TryExportFullMix(Path.Combine(temp, "mix"), out var mixPath))
                {
                    error = "Could not find a full mix in the song stem.";
                    return false;
                }

                if (!TryMakeStereoMix(song, mixPath, Path.Combine(temp, "stereo.wav"), out var stereoPath, out error))
                {
                    return false;
                }

                string outDir = Path.Combine(temp, "out");
                if (!RunDemucs(stereoPath, outDir, out error))
                {
                    return false;
                }

                string stemDir = FindSeparatedDirectory(outDir);
                if (stemDir == null)
                {
                    error = "Demucs did not write vocals, bass, drums, and other.";
                    return false;
                }

                if (!song.TryInstallDemucsStems(
                    Path.Combine(stemDir, "vocals.wav"),
                    Path.Combine(stemDir, "bass.wav"),
                    Path.Combine(stemDir, "drums.wav"),
                    Path.Combine(stemDir, "other.wav")))
                {
                    error = "Could not save the generated stems.";
                    return false;
                }

                error = null;
                return true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                    {
                        Directory.Delete(temp, true);
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Failed to delete Demucs temp files");
                }
            }
        }

        private static bool TryMakeStereoMix(SongEntry song, string mixPath, string destination, out string stereoPath, out string error)
        {
            stereoPath = destination;
            // Demucs keeps only the first two channels of a packed mogg. On a CON those are the
            // silent drum pair, and a silent file makes its pad check throw AssertionError.
            string filterArgs = song.TryGetSongStemChannels(out var channels) && channels.Length > 0
                ? "-af " + Quote(BuildPanFilter(channels)) + " "
                : "-ac 2 ";
            var start = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-y -hide_banner -loglevel error -i " + Quote(mixPath) + " " + filterArgs + "-ar 44100 " + Quote(destination),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process process;
            try
            {
                process = new Process { StartInfo = start };
                if (!process.Start())
                {
                    error = "ffmpeg did not start.";
                    return false;
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is FileNotFoundException)
            {
                error = "ffmpeg was not found. It is required to prepare the mix.";
                return false;
            }

            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    stderr.AppendLine(args.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Dispose();

            if (exitCode == 0 && File.Exists(destination) && new FileInfo(destination).Length > MinimumMixBytes)
            {
                error = null;
                return true;
            }

            if (stderr.Length > 0)
            {
                YargLogger.LogError("ffmpeg failed to prepare the Demucs mix: " + stderr);
            }

            error = "Could not convert the mix to stereo.";
            return false;
        }

        private static string BuildPanFilter(int[] channels)
        {
            if (channels.Length == 1)
            {
                string channel = "c" + channels[0];
                return "pan=stereo|c0=" + channel + "|c1=" + channel;
            }

            if (channels.Length == 2)
            {
                return "pan=stereo|c0=c" + channels[0] + "|c1=c" + channels[1];
            }

            var left = new StringBuilder();
            var right = new StringBuilder();
            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < channels.Length; i++)
            {
                bool toLeft = i % 2 == 0;
                var side = toLeft ? left : right;
                if (toLeft)
                {
                    if (leftCount > 0)
                    {
                        side.Append('+');
                    }

                    leftCount++;
                }
                else
                {
                    if (rightCount > 0)
                    {
                        side.Append('+');
                    }

                    rightCount++;
                }

                side.Append('c').Append(channels[i]);
            }

            return "pan=stereo|c0=" + left + "|c1=" + right;
        }

        private static string SummarizeError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Unknown error";
            }

            string last = null;
            foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("File ", StringComparison.Ordinal) ||
                    line.StartsWith("Traceback", StringComparison.Ordinal) ||
                    line.IndexOf("site-packages", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                last = line;
            }

            last ??= text.Trim();
            return last.Length > 180 ? last.Substring(0, 180) : last;
        }

        private static bool RunDemucs(string mixPath, string outDir, out string error)
        {
            Directory.CreateDirectory(outDir);
            int[] order = _launcher >= 0 ? new[] { _launcher } : new[] { 0, 1, 2 };
            string lastError = "demucs was not found. Install it and make sure it is on PATH.";
            foreach (int launcher in order)
            {
                Process process;
                try
                {
                    process = CreateProcess(launcher, mixPath, outDir);
                    if (!process.Start())
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is FileNotFoundException)
                {
                    lastError = ex.Message;
                    if (_launcher == launcher)
                    {
                        _launcher = -1;
                        return RunDemucs(mixPath, outDir, out error);
                    }

                    continue;
                }

                var tail = new StringBuilder();
                process.OutputDataReceived += (_, _) => { };
                process.ErrorDataReceived += (_, args) => AppendTail(tail, args.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode == 0 && FindSeparatedDirectory(outDir) != null)
                {
                    _launcher = launcher;
                    error = null;
                    return true;
                }

                lastError = tail.Length > 0 ? tail.ToString().Trim() : $"demucs exited with code {process.ExitCode}.";
                if (IsMissingDemucs(lastError))
                {
                    if (_launcher == launcher)
                    {
                        _launcher = -1;
                        return RunDemucs(mixPath, outDir, out error);
                    }

                    continue;
                }

                error = lastError;
                return false;
            }

            error = lastError;
            return false;
        }

        private static bool IsMissingDemucs(string error)
        {
            return error.IndexOf("No module named demucs", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("is not recognized as an internal or external command", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Process CreateProcess(int launcher, string mixPath, string outDir)
        {
            var start = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            string demucsArgs = "-o " + Quote(outDir) + " " + Quote(mixPath);
            switch (launcher)
            {
                case 0:
                    start.FileName = "demucs";
                    start.Arguments = demucsArgs;
                    break;
                case 1:
                    start.FileName = "py";
                    start.Arguments = "-m demucs " + demucsArgs;
                    break;
                default:
                    start.FileName = "python";
                    start.Arguments = "-m demucs " + demucsArgs;
                    break;
            }

            return new Process { StartInfo = start };
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FindSeparatedDirectory(string outDir)
        {
            if (!Directory.Exists(outDir))
            {
                return null;
            }

            foreach (var directory in Directory.EnumerateDirectories(outDir, "*", SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(directory, "vocals.wav")) &&
                    File.Exists(Path.Combine(directory, "bass.wav")) &&
                    File.Exists(Path.Combine(directory, "drums.wav")) &&
                    File.Exists(Path.Combine(directory, "other.wav")))
                {
                    return directory;
                }
            }

            return null;
        }

        private static void AppendTail(StringBuilder tail, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            lock (tail)
            {
                if (tail.Length > 1200)
                {
                    tail.Remove(0, tail.Length - 800);
                }

                tail.AppendLine(line);
            }
        }
    }
}
