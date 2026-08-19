#nullable enable
using System.IO;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Loads sound effect samples from streaming assets and creates their playback channels.
    /// </summary>
    internal sealed class BassSampleLoader
    {
        private readonly string[]        _formats;
        private readonly BassAudioRouter _router;

        internal BassSampleLoader(BassAudioRouter router, string[] formats)
        {
            _router = router;
            _formats = formats;
        }

        internal SampleChannel[] LoadSfx()
        {
            var samples = new SampleChannel[AudioHelpers.SfxSamples.Count];
            string sfxFolder = Path.Combine(Application.streamingAssetsPath, "sfx");

            foreach (var sample in AudioHelpers.SfxSamples)
            {
                string? path = FindSamplePath(sfxFolder, sample.File);
                if (path == null)
                {
                    continue;
                }

                var kind = sample.Kind;
                var channel = BassSampleChannel.Create(kind, path, _router,
                    BassOutputChannel.Create(SettingsManager.Settings?.OutputChannelSfx.Value ?? 0), sample.CanLoop);
                if (channel != null)
                {
                    samples[(int) kind] = channel;
                    YargLogger.LogFormatInfo("Loaded {0}", sample.File);
                }
            }

            return samples;
        }

        internal DrumSampleChannel[] LoadDrumSfx()
        {
            var samples = new DrumSampleChannel[AudioHelpers.DrumSamples.Count];
            string sfxFolder = Path.Combine(Application.streamingAssetsPath, "drumSfx");

            foreach (var sample in AudioHelpers.DrumSamples)
            {
                string? path = FindSamplePath(sfxFolder, sample.File);
                if (path == null)
                {
                    continue;
                }

                var kind = sample.Kind;
                var channel = BassDrumSampleChannel.Create(kind, path, _router,
                    BassOutputChannel.Create(SettingsManager.Settings?.OutputChannelDrumSfx.Value ?? 0));
                if (channel != null)
                {
                    samples[(int) kind] = channel;
                }
            }

            return samples;
        }

        internal VoxSampleChannel[] LoadVox()
        {
            var samples = new VoxSampleChannel[AudioHelpers.VoxSamples.Count];
            string voxFolder = Path.Combine(Application.streamingAssetsPath, "vox");

            foreach (var sample in AudioHelpers.VoxSamples)
            {
                string? path = FindSamplePath(voxFolder, sample.File);
                if (path == null)
                {
                    continue;
                }

                var kind = sample.Kind;
                var channel = BassVoxSampleChannel.Create(kind, path, _router,
                    BassOutputChannel.Create(SettingsManager.Settings?.OutputChannelVox.Value ?? 0));
                if (channel != null)
                {
                    samples[(int) kind] = channel;
                }
            }

            return samples;
        }

        internal MetronomeSampleChannel[] LoadMetronome()
        {
            var samples = new MetronomeSampleChannel[AudioHelpers.MetronomeSamples.Count];
            string metronomeFolder = Path.Combine(Application.streamingAssetsPath, "metronome");

            foreach (var sample in AudioHelpers.MetronomeSamples)
            {
                string? highPath = FindSamplePath(metronomeFolder, sample.File);
                string? lowPath = FindSamplePath(metronomeFolder, sample.AlternateFile);
                if (highPath == null || lowPath == null)
                {
                    continue;
                }

                var kind = sample.Kind;
                int channelId = SettingsManager.Settings?.OutputChannelMetronome.Value ?? -1;
                if (channelId == -1)
                {
                    channelId = SettingsManager.Settings?.OutputChannelDefault.Value ?? 0;
                }

                var channel = BassMetronomeSampleChannel.Create(kind, highPath, lowPath, _router,
                    BassOutputChannel.Create(channelId));
                if (channel != null)
                {
                    samples[(int) kind] = channel;
                }
            }

            return samples;
        }

        internal BassVenueSampleChannel?
            CreateVenueSample(string name, byte[] sampleData, OutputChannel? outputChannel) =>
            BassVenueSampleChannel.Create(name, sampleData, _router, outputChannel);

        private string? FindSamplePath(string folder, string file)
        {
            string pathWithoutExtension = Path.Combine(folder, file);
            foreach (string format in _formats)
            {
                string path = pathWithoutExtension + format;
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }
}