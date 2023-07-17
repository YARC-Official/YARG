using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using UnityEngine;
using YARG.Core;

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
                MidIOHelper.GUITAR_TRACK, Instrument.FiveFretGuitar
            },
            {
                MidIOHelper.GH1_GUITAR_TRACK, Instrument.FiveFretGuitar
            },
            {
                MidIOHelper.GUITAR_COOP_TRACK, Instrument.FiveFretCoopGuitar
            },
            {
                MidIOHelper.BASS_TRACK, Instrument.FiveFretBass
            },
            {
                MidIOHelper.RHYTHM_TRACK, Instrument.FiveFretRhythm
            },
            {
                MidIOHelper.DRUMS_TRACK, Instrument.FourLaneDrums
            },
            {
                "PART DRUM", Instrument.FourLaneDrums
            },
            {
                MidIOHelper.KEYS_TRACK, Instrument.Keys
            },
            {
                MidIOHelper.VOCALS_TRACK, Instrument.Vocals
            },
            {
                "PART REAL_GUITAR", Instrument.ProGuitar_17Fret
            },
            {
                "PART REAL_BASS", Instrument.ProBass_17Fret
            },
            {
                "HARM1", Instrument.Harmony
            },
            {
                "HARM2", Instrument.Harmony
            },
            {
                "HARM3", Instrument.Harmony
            },
            {
                "PART HARM1", Instrument.Harmony
            },
            {
                "PART HARM2", Instrument.Harmony
            },
            {
                "PART HARM3", Instrument.Harmony
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