#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// One PipeWire Audio/Source node backed by ALSA hardware, as reported by
    /// the native yarg_audio snapshot (see yarg_audio.h).
    /// </summary>
    internal readonly struct PipeWireSourceInfo
    {
        public readonly int AlsaCard;
        public readonly int AlsaDevice;
        public readonly int AlsaSubdevice;
        public readonly int CaptureChannel;
        public readonly int CaptureChannels;
        public readonly string NodeName;
        public readonly string Description;
        public readonly string AlsaPath;

        public PipeWireSourceInfo(int alsaCard, int alsaDevice, int alsaSubdevice,
            int captureChannel, int captureChannels, string nodeName, string description, string alsaPath)
        {
            AlsaCard = alsaCard;
            AlsaDevice = alsaDevice;
            AlsaSubdevice = alsaSubdevice;
            CaptureChannel = captureChannel;
            CaptureChannels = captureChannels;
            NodeName = nodeName;
            Description = description;
            AlsaPath = alsaPath;
        }
    }

    /// <summary>
    /// PipeWire input source snapshot (Linux only). The native library dlopens
    /// libpipewire-0.3, so a machine without PipeWire degrades to a null
    /// result and the caller falls back to plain BASS enumeration.
    /// </summary>
    internal static class PipeWireInputDeviceSource
    {
        private const uint ABI_VERSION = 1;

        private const int MAX_INPUT_SOURCES = 32;
        private const int NODE_NAME_MAX = 256;
        private const int DESCRIPTION_MAX = 256;
        private const int ALSA_PATH_MAX = 128;

        private static List<PipeWireSourceInfo>? _cachedSources;
        private static bool _cacheAttempted;

        /// <summary>
        /// Returns the per-session PipeWire source snapshot, or null when
        /// PipeWire is unavailable (non-Linux platform, missing library, no
        /// server) or the snapshot failed.
        /// </summary>
        public static List<PipeWireSourceInfo>? GetSources()
        {
            if (_cacheAttempted)
            {
                return _cachedSources;
            }
            _cacheAttempted = true;
            _cachedSources = QuerySources();
            return _cachedSources;
        }

        private static List<PipeWireSourceInfo>? QuerySources()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            try
            {
                if (Native.GetAbiVersion() != ABI_VERSION)
                {
                    YargLogger.LogWarning(
                        "yarg_audio ABI mismatch; PipeWire device listing disabled.");
                    return null;
                }

                int result = ListNative(out var sources);
                if (result == 0)
                {
                    YargLogger.LogFormatInfo("PipeWire input snapshot: {0} source(s)", sources.Count);
                    return sources;
                }

                if (result == -4)
                {
                    YargLogger.LogInfo(
                        "PipeWire unavailable; falling back to BASS device listing.");
                }
                else
                {
                    YargLogger.LogFormatWarning(
                        "PipeWire input snapshot failed with result {0}; falling back to BASS device listing.", result);
                }
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception,
                    "Failed to load PipeWire input snapshot; falling back to BASS device listing.");
                return null;
            }
#else
            return null;
#endif
        }

        private static unsafe int ListNative(out List<PipeWireSourceInfo> sources)
        {
            sources = new List<PipeWireSourceInfo>();

            int snapshotSize = SNAPSHOT_SIZE;
            IntPtr buffer = Marshal.AllocHGlobal(snapshotSize);
            try
            {
                Marshal.WriteInt32(buffer, 0, snapshotSize);
                int result = Native.ListInputSources(buffer);
                if (result != 0)
                {
                    return result;
                }

                uint count = (uint) Marshal.ReadInt32(buffer, 4);
                if (count > MAX_INPUT_SOURCES)
                {
                    count = MAX_INPUT_SOURCES;
                }

                var native = (NativeSource*) (buffer + SNAPSHOT_HEADER_SIZE);
                for (uint i = 0; i < count; ++i)
                {
                    var source = native[i];
                    sources.Add(new PipeWireSourceInfo(
                        source.AlsaCard,
                        source.AlsaDevice,
                        source.AlsaSubdevice,
                        source.CaptureChannel,
                        source.CaptureChannels,
                        ReadString(source.NodeName, NODE_NAME_MAX),
                        ReadString(source.Description, DESCRIPTION_MAX),
                        ReadString(source.AlsaPath, ALSA_PATH_MAX)));
                }
                return 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static unsafe string ReadString(byte* buffer, int maxLength)
        {
            int length = 0;
            while (length < maxLength && buffer[length] != 0)
            {
                ++length;
            }
            return Encoding.UTF8.GetString(buffer, length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct NativeSource
        {
            public uint Size;
            public int AlsaCard;
            public int AlsaDevice;
            public int AlsaSubdevice;
            public int CaptureChannel;
            public int CaptureChannels;
            public fixed byte NodeName[NODE_NAME_MAX];
            public fixed byte Description[DESCRIPTION_MAX];
            public fixed byte AlsaPath[ALSA_PATH_MAX];
        }

        private const int NATIVE_SOURCE_SIZE =
            sizeof(uint) + 5 * sizeof(int) + NODE_NAME_MAX + DESCRIPTION_MAX + ALSA_PATH_MAX;
        private const int SNAPSHOT_HEADER_SIZE = 2 * sizeof(uint);
        private const int SNAPSHOT_SIZE =
            SNAPSHOT_HEADER_SIZE + MAX_INPUT_SOURCES * NATIVE_SOURCE_SIZE;

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_audio_get_abi_version",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetAbiVersion();

            [DllImport(LIBRARY, EntryPoint = "yarg_audio_list_input_sources",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int ListInputSources(IntPtr snapshot);
        }
    }
}
