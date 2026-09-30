using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ManagedBass;
using YARG.Audio.BASS;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Persistent;

namespace YARG.Song
{
    /// <summary>
    /// CON packages list drums, bass, and vocals even when those channels are silent
    /// and the whole mix is in the song stem. This listens to a short slice of each
    /// song once and remembers the result.
    /// </summary>
    public static class StemSilenceScanner
    {
        private const float SilentPeak = 0.001f;
        private const BassFlags ProbeFlags = BassFlags.Float | BassFlags.Decode | (BassFlags) 64;
        private static readonly double[] ProbeSeconds = { 20, 45, 90, 120 };

        private static readonly object Gate = new();
        private static bool _started;
        private static bool _announced;
        private static Dictionary<string, int> _cache = new();
        private static Action _onUpdated;

        private static string CachePath => Path.Combine(PathHelper.PersistentDataPath, "stem-silence-cache.txt");

        public static void WarmCache()
        {
            lock (Gate)
            {
                if (_cache.Count == 0)
                {
                    _cache = LoadCache();
                }
            }

            ApplyCache(SongContainer.Songs);
        }

        public static void Start(IReadOnlyList<SongEntry> priority, Action onUpdated)
        {
            _onUpdated = onUpdated;
            WarmCache();

            List<SongEntry> pending;
            lock (Gate)
            {
                pending = CollectPending(priority);
                if (pending.Count == 0)
                {
                    return;
                }

                if (!_announced)
                {
                    _announced = true;
                    ToastManager.ToastInformation(Localize.Key("Menu.Toast.StemsChecking"));
                }

                if (_started)
                {
                    return;
                }

                _started = true;
            }

            Task.Run(() => Scan(pending));
        }

        private static void Scan(List<SongEntry> pending)
        {
            int found = 0;
            int sinceSave = 0;
            foreach (var song in pending)
            {
                if (!song.NeedsStemSilenceProbe())
                {
                    continue;
                }

                bool missing = false;
                bool probed = false;
                try
                {
                    probed = TryProbe(song, out missing);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Failed to check whether a song has stems");
                }

                if (!probed)
                {
                    song.SetProbedStemSeparation(StemSeparation.Present);
                    continue;
                }

                var state = missing ? StemSeparation.Missing : StemSeparation.Present;
                song.SetProbedStemSeparation(state);
                Remember(song, missing ? 1 : 0);
                sinceSave++;
                if (missing)
                {
                    found++;
                }

                if (sinceSave >= 12)
                {
                    sinceSave = 0;
                    SaveCache();
                    Notify();
                }
            }

            SaveCache();
            Notify();
            if (found > 0)
            {
                YargLogger.LogFormatInfo("Marked {0} songs as having no stems", found);
            }
        }

        private static bool TryProbe(SongEntry song, out bool missing)
        {
            missing = false;
            if (!song.TryOpenOggAudio(out var audio, out var drums, out var bass, out var vocals))
            {
                return false;
            }

            var procedures = new BassStreamProcedures(audio);
            int handle = 0;
            try
            {
                handle = Bass.CreateStream(StreamSystem.NoBuffer, ProbeFlags, procedures);
                if (handle == 0)
                {
                    audio.Dispose();
                    return false;
                }

                var info = Bass.ChannelGetInfo(handle);
                if (info.Frequency <= 0 || info.Channels <= 0)
                {
                    return false;
                }

                var peaks = new float[info.Channels];
                bool heardAnything = false;
                foreach (double seconds in ProbeSeconds)
                {
                    if (!Measure(handle, info, seconds, peaks))
                    {
                        continue;
                    }

                    if (!HasEnergy(peaks))
                    {
                        continue;
                    }

                    heardAnything = true;
                    missing = GroupSilent(drums, peaks) && GroupSilent(bass, peaks) && GroupSilent(vocals, peaks);
                    break;
                }

                return heardAnything;
            }
            finally
            {
                if (handle != 0)
                {
                    Bass.StreamFree(handle);
                }
            }
        }

        private static bool Measure(int handle, ChannelInfo info, double seconds, float[] peaks)
        {
            long bytePosition = (long) (seconds * info.Frequency * info.Channels * sizeof(float));
            if (!Bass.ChannelSetPosition(handle, bytePosition, PositionFlags.Bytes))
            {
                return false;
            }

            int sampleCount = info.Frequency * info.Channels;
            var buffer = new float[sampleCount];
            int read = Bass.ChannelGetData(handle, buffer, sampleCount * sizeof(float));
            if (read <= 0)
            {
                return false;
            }

            Array.Clear(peaks, 0, peaks.Length);
            int frames = read / sizeof(float) / info.Channels;
            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * info.Channels;
                for (int channel = 0; channel < info.Channels; channel++)
                {
                    float sample = Math.Abs(buffer[offset + channel]);
                    if (sample > peaks[channel])
                    {
                        peaks[channel] = sample;
                    }
                }
            }

            return true;
        }

        private static bool HasEnergy(float[] peaks)
        {
            for (int i = 0; i < peaks.Length; i++)
            {
                if (peaks[i] >= SilentPeak)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GroupSilent(int[] channels, float[] peaks)
        {
            if (channels == null || channels.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < channels.Length; i++)
            {
                int channel = channels[i];
                if ((uint) channel < (uint) peaks.Length && peaks[channel] >= SilentPeak)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<SongEntry> CollectPending(IReadOnlyList<SongEntry> priority)
        {
            var pending = new List<SongEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPending(priority, pending, seen);
            AddPending(SongContainer.Songs, pending, seen);
            return pending;
        }

        private static void AddPending(IReadOnlyList<SongEntry> songs, List<SongEntry> pending, HashSet<string> seen)
        {
            if (songs == null)
            {
                return;
            }

            for (int i = 0; i < songs.Count; i++)
            {
                var song = songs[i];
                if (song == null || !seen.Add(song.ActualLocation))
                {
                    continue;
                }

                if (song.NeedsStemSilenceProbe())
                {
                    pending.Add(song);
                }
            }
        }

        private static void ApplyCache(IReadOnlyList<SongEntry> songs)
        {
            if (songs == null)
            {
                return;
            }

            for (int i = 0; i < songs.Count; i++)
            {
                var song = songs[i];
                if (song == null)
                {
                    continue;
                }

                int state;
                lock (Gate)
                {
                    if (!_cache.TryGetValue(CacheKey(song), out state))
                    {
                        continue;
                    }
                }

                song.SetProbedStemSeparation(state == 1 ? StemSeparation.Missing : StemSeparation.Present);
            }
        }

        private static void Remember(SongEntry song, int state)
        {
            lock (Gate)
            {
                _cache[CacheKey(song)] = state;
            }
        }

        private static void Notify()
        {
            var updated = _onUpdated;
            if (updated == null)
            {
                return;
            }

            UnityMainThreadCallback.QueueEvent(updated);
        }

        private static Dictionary<string, int> LoadCache()
        {
            var cache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(CachePath))
            {
                return cache;
            }

            try
            {
                foreach (var line in File.ReadLines(CachePath))
                {
                    int split = line.IndexOf('\t');
                    if (split <= 0 || split >= line.Length - 1)
                    {
                        continue;
                    }

                    if (!int.TryParse(line.Substring(0, split), NumberStyles.Integer, CultureInfo.InvariantCulture, out int state))
                    {
                        continue;
                    }

                    cache[line.Substring(split + 1)] = state;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to read the stem silence cache");
            }

            return cache;
        }

        private static void SaveCache()
        {
            try
            {
                Dictionary<string, int> copy;
                lock (Gate)
                {
                    copy = new Dictionary<string, int>(_cache, StringComparer.OrdinalIgnoreCase);
                }

                var builder = new StringBuilder();
                foreach (var pair in copy)
                {
                    builder.Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                    builder.Append('\t');
                    builder.AppendLine(pair.Key);
                }

                File.WriteAllText(CachePath, builder.ToString());
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to save the stem silence cache");
            }
        }

        private static string CacheKey(SongEntry song)
        {
            return song.GetLastWriteTime().Ticks.ToString(CultureInfo.InvariantCulture) + "|" + song.ActualLocation;
        }
    }
}
