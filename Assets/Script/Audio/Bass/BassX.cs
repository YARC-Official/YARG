using System;
using System.IO;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Utility methods and assertions for BASS calls, providing consistent error checking,
    ///     exception throwing, and thread configuration.
    /// </summary>
    internal static class BassX
    {
        // Undocumented BASS attribute for setting a mixer's maximum processing thread count.
        private const ChannelAttribute PROCESSING_THREADS_ATTRIBUTE = (ChannelAttribute) 86017;

        internal static int CreateSourceUnchecked(Stream stream)
        {
            // Last flag is BASS_SAMPLE_NOREORDER, which is not yet included in BassFlags.
            // https://www.un4seen.com/forum/?topic=20148.msg140872#msg140872
            const BassFlags FLAGS = BassFlags.Prescan | BassFlags.Decode | BassFlags.AsyncFile | (BassFlags) 64;
            return Bass.CreateStream(StreamSystem.NoBuffer, FLAGS, new BassStreamProcedures(stream));
        }

        public static bool SetProcessingThreads(int mixer, int count) =>
            Check(Bass.ChannelSetAttribute(mixer, PROCESSING_THREADS_ATTRIBUTE, count),
                $"set processing threads for mixer {mixer}");

        public static void Require(bool success, string operation)
        {
            if (!success)
            {
                throw CreateException(operation);
            }
        }

        public static int Require(int handle, string operation)
        {
            if (handle == 0)
            {
                throw CreateException(operation);
            }

            return handle;
        }

        public static bool Check(bool success, string operation)
        {
            if (success)
            {
                return true;
            }

            var error = Bass.LastError;
            YargLogger.LogFormatError("Failed to {0}: {1}", operation, error);
            return false;
        }

        private static BassOperationException CreateException(string operation) => new(operation, Bass.LastError);

        internal sealed class BassOperationException : Exception
        {
            public BassOperationException(string operation, Errors error) : base($"Failed to {operation}: {error}")
            {
            }
        }
    }
}
