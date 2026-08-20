#nullable enable
using System;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Contract for a live microphone input source (ASIO or Shared Audio) that supplies audio samples
    ///     to BassMicAnalyzer and routes monitoring audio through BassMicSignal.
    /// </summary>
    internal interface IBassMicSource : IBassMicSampleSource, IDisposable
    {
        string DisplayName { get; }
        string BaseName    { get; }
        int    Channel     { get; }

        event Action? InputChanged;

        bool TryCreateRecordingChannel(bool withEffects, out int handle, out int sampleRate);
        void ReleaseRecordingChannel(int handle);
        bool SetMonitoringLevel(float volume);
        bool SetReverbLevel(float wet);
        bool Reset();
        MicBufferInfo? GetBufferInfo();
    }
}
