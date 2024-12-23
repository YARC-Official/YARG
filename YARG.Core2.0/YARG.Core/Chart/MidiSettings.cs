using System.Text;
using Melanchall.DryWetMidi.Core;
using YARG.Core.IO;

namespace YARG.Core.Chart
{
    public static class MidiSettings
    {
        public static readonly ReadingSettings Instance = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
            TextEncoding = YARGTextReader.UTF8Strict,
        };
	}

    public static class MidiSettingsLatin1
    {
        public static readonly ReadingSettings Instance = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
            TextEncoding = YARGTextReader.Latin1,
        };
	}
}