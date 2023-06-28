using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using UnityEngine;
using YARG.Data;

namespace YARG.Song.Preparsers
{
    public static class MidPreparser
    {
        private static readonly ReadingSettings ReadSettings = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
        };

        private static readonly IReadOnlyDictionary<string, Instrument> PartLookup = new Dictionary<string, Instrument>
        {
            {
                MidIOHelper.GUITAR_TRACK, Instrument.GUITAR
            },
            {
                MidIOHelper.GH1_GUITAR_TRACK, Instrument.GUITAR
            },
            {
                MidIOHelper.GUITAR_COOP_TRACK, Instrument.GUITAR_COOP
            },
            {
                MidIOHelper.BASS_TRACK, Instrument.BASS
            },
            {
                MidIOHelper.RHYTHM_TRACK, Instrument.RHYTHM
            },
            {
                MidIOHelper.DRUMS_TRACK, Instrument.DRUMS
            },
            {
                "PART DRUM", Instrument.DRUMS
            },
            {
                MidIOHelper.KEYS_TRACK, Instrument.KEYS
            },
            {
                MidIOHelper.VOCALS_TRACK, Instrument.VOCALS
            },
            {
                "PART REAL_GUITAR", Instrument.REAL_GUITAR
            },
            {
                "PART REAL_BASS", Instrument.REAL_BASS
            },
            {
                "HARM1", Instrument.HARMONY
            },
            {
                "HARM2", Instrument.HARMONY
            },
            {
                "HARM3", Instrument.HARMONY
            },
            {
                "PART HARM1", Instrument.HARMONY
            },
            {
                "PART HARM2", Instrument.HARMONY
            },
            {
                "PART HARM3", Instrument.HARMONY
            },
        };

        public static bool GetAvailableTracks(byte[] chartData, out ulong tracks)
        {
            using var stream = new MemoryStream(chartData);
            try
            {
                var midi = MidiFile.Read(stream, ReadSettings);
                tracks = ReadStream(midi);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                tracks = 0;
                return false;
            }
        }

        public static bool GetAvailableTracks(SongEntry song, out ulong tracks)
        {
            try
            {
                var midi = MidiFile.Read(Path.Combine(song.Location, song.NotesFile), ReadSettings);
                tracks = ReadStream(midi);
                return true;
            }
            catch
            {
                tracks = 0;
                return false;
            }
        }

        private static ulong ReadStream(MidiFile midi)
        {
            ulong tracks = 0;

            foreach (var chunk in midi.GetTrackChunks())
            {
                foreach (var trackEvent in chunk.Events)
                {
                    if (trackEvent is not SequenceTrackNameEvent trackName)
                    {
                        continue;
                    }

                    string trackNameKey = trackName.Text.ToUpper();

                    if (!PartLookup.TryGetValue(trackNameKey, out var instrument))
                    {
                        continue;
                    }

                    int shiftAmount = (int) instrument * 4;
                    tracks |= 0xFUL << shiftAmount;
                }
            }

            return tracks;
        }
    }
}