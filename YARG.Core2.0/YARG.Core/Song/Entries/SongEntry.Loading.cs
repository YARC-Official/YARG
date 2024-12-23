using System;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public class BackgroundResult : IDisposable
    {
        public readonly BackgroundType Type;
        public readonly Stream? Stream;
        public readonly YARGImage? Image;

        public BackgroundResult(BackgroundType type, Stream stream)
        {
            Type = type;
            Stream = stream;
        }

        public BackgroundResult(YARGImage? image)
        {
            Type = BackgroundType.Image;
            Image = image;
        }

        public void Dispose()
        {
            Stream?.Dispose();
            Image?.Dispose();
        }
    }

    public abstract partial class SongEntry
    {
        public abstract SongChart? LoadChart();
        public abstract StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems);
        public abstract StemMixer? LoadPreviewAudio(float speed);
        public abstract YARGImage? LoadAlbumData();
        public abstract BackgroundResult? LoadBackground(BackgroundType options);
        public abstract FixedArray<byte> LoadMiloData();
    }
}
