using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public abstract class RBProUpgrade
    {
        public abstract DateTime LastUpdatedTime { get; }
        public abstract void WriteToCache(MemoryStream stream);
        public abstract Stream? GetUpgradeMidiStream();
        public abstract FixedArray<byte> LoadUpgradeMidi();
    }

    [Serializable]
    public sealed class PackedRBProUpgrade : RBProUpgrade
    {
        private readonly CONFileListing? _midiListing;
        private readonly DateTime _lastUpdatedTime;

        public override DateTime LastUpdatedTime => _lastUpdatedTime;

        public PackedRBProUpgrade(CONFileListing? listing, DateTime lastWrite)
        {
            _midiListing = listing;
            _lastUpdatedTime = listing?.LastWrite ?? lastWrite;
        }

        public override void WriteToCache(MemoryStream stream)
        {
            stream.Write(_lastUpdatedTime.ToBinary(), Endianness.Little);
        }

        public override Stream? GetUpgradeMidiStream()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return null;
            }
            return _midiListing.CreateStream();
        }

        public override FixedArray<byte> LoadUpgradeMidi()
        {
            if (_midiListing == null || !_midiListing.ConFile.IsStillValid())
            {
                return FixedArray<byte>.Null;
            }
            return _midiListing.LoadAllBytes();
        }
    }

    [Serializable]
    public sealed class UnpackedRBProUpgrade : RBProUpgrade
    {
        private readonly AbridgedFileInfo _midi;
        public override DateTime LastUpdatedTime => _midi.LastUpdatedTime;

        public UnpackedRBProUpgrade(in AbridgedFileInfo info)
        {
            _midi = info;
        }

        public override void WriteToCache(MemoryStream stream)
        {
            stream.Write(_midi.LastUpdatedTime.ToBinary(), Endianness.Little);
        }

        public override Stream? GetUpgradeMidiStream()
        {
            return _midi.IsStillValid() ? new FileStream(_midi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        }

        public override FixedArray<byte> LoadUpgradeMidi()
        {
            return _midi.IsStillValid() ? FixedArray<byte>.Load(_midi.FullName) : FixedArray<byte>.Null;
        }
    }
}
