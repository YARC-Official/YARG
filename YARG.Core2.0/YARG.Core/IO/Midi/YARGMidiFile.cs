using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public sealed class YARGMidiFile : IEnumerable<YARGMidiTrack>
    {
        private static readonly FourCC HEADER_TAG = new('M', 'T', 'h', 'd');
        private static readonly FourCC TRACK_TAG = new('M', 'T', 'r', 'k');

        private readonly Stream _stream;
        public readonly ushort Format;
        public readonly ushort NumTracks;
        public readonly ushort Resolution;

        private ushort _trackNumber = 0;
        public ushort TrackNumber => _trackNumber;

        private const int SIZEOF_HEADER = 6;
        public YARGMidiFile(Stream stream)
        {
            _stream = stream;
            if (!HEADER_TAG.Matches(stream))
                throw new Exception("Midi Header Chunk Tag 'MThd' not found");

            int length = stream.Read<int>(Endianness.Big);
            if (length < SIZEOF_HEADER)
                throw new Exception("Midi Header not of sufficient length");

            long next = stream.Position + length;
            Format = stream.Read<ushort>(Endianness.Big);
            NumTracks = stream.Read<ushort>(Endianness.Big);
            Resolution = stream.Read<ushort>(Endianness.Big);
            stream.Position = next;
        }

        public YARGMidiTrack? LoadNextTrack()
        {
            if (_trackNumber == NumTracks || _stream.Position == _stream.Length)
                return null;

            _trackNumber++;
            if (!TRACK_TAG.Matches(_stream))
            {
                throw new Exception($"Midi Track Tag 'MTrk' not found for Track '{_trackNumber}'");
            }

            try
            {
                return new YARGMidiTrack(_stream);
            }
            catch (UnauthorizedAccessException ex)
            {
                YargLogger.LogException(ex, "Coding note: Ensure that buffer access is enabled when you construct the memorystream");
                return null;
            }
        }

        public IEnumerator<YARGMidiTrack> GetEnumerator()
        {
            return new MidiFileEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class MidiFileEnumerator : IEnumerator<YARGMidiTrack>
        {
            private readonly YARGMidiFile file;
            private YARGMidiTrack? _current;
            public MidiFileEnumerator(YARGMidiFile file)
            {
                this.file = file;
            }

            public YARGMidiTrack Current => _current!;

            object IEnumerator.Current => _current!;

            public bool MoveNext()
            {
                _current?.Dispose();
                _current = file.LoadNextTrack();
                return _current != null;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                _current?.Dispose();
            }
        }
    }
}
