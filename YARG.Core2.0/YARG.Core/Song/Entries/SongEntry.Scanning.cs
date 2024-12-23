using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public abstract partial class SongEntry
    {
        protected static (ScanResult Result, long Resolution) ParseMidi(in FixedArray<byte> file, DrumPreparseHandler drums, ref AvailableParts parts)
        {
            var midiFile = new YARGMidiFile(file.ToStream());
            if (midiFile.Resolution == 0)
            {
                return (ScanResult.InvalidResolution, 0);
            }

            bool harm2 = false;
            bool harm3 = false;
            foreach (var track in midiFile)
            {
                if (midiFile.TrackNumber == 1)
                    continue;

                var trackname = track.FindTrackName(Encoding.ASCII);
                if (trackname == null)
                {
                    return (ScanResult.MultipleMidiTrackNames, 0);
                }

                if (!YARGMidiTrack.TRACKNAMES.TryGetValue(trackname, out var type))
                {
                    continue;
                }

                switch (type)
                {
                    case MidiTrackType.Guitar_5: if (!parts.FiveFretGuitar.WasParsed())     parts.FiveFretGuitar.Difficulties     = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_5:   if (!parts.FiveFretBass.WasParsed())       parts.FiveFretBass.Difficulties       = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_5: if (!parts.FiveFretRhythm.WasParsed())     parts.FiveFretRhythm.Difficulties     = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_5:   if (!parts.FiveFretCoopGuitar.WasParsed()) parts.FiveFretCoopGuitar.Difficulties = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Keys:     if (!parts.Keys.WasParsed())               parts.Keys.Difficulties               = Midi_FiveFret_Preparser.Parse(track); break;

                    case MidiTrackType.Guitar_6: if (!parts.SixFretGuitar.WasParsed())      parts.SixFretGuitar.Difficulties      = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_6:   if (!parts.SixFretBass.WasParsed())        parts.SixFretBass.Difficulties        = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_6: if (!parts.SixFretRhythm.WasParsed())      parts.SixFretRhythm.Difficulties      = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_6:   if (!parts.SixFretCoopGuitar.WasParsed())  parts.SixFretCoopGuitar.Difficulties  = Midi_SixFret_Preparser.Parse(track); break;

                    case MidiTrackType.Drums: drums.ParseMidi(track); break;

                    case MidiTrackType.EliteDrums: if (!parts.EliteDrums.WasParsed()) parts.EliteDrums.Difficulties = Midi_EliteDrums_Preparser.Parse(track); break;

                    case MidiTrackType.Pro_Guitar_17: if (!parts.ProGuitar_17Fret.WasParsed()) parts.ProGuitar_17Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Guitar_22: if (!parts.ProGuitar_22Fret.WasParsed()) parts.ProGuitar_22Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;
                    case MidiTrackType.Pro_Bass_17:   if (!parts.ProBass_17Fret.WasParsed())   parts.ProBass_17Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Bass_22:   if (!parts.ProBass_22Fret.WasParsed())   parts.ProBass_22Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;

                    case MidiTrackType.Pro_Keys_E: if (!parts.ProKeys[Difficulty.Easy]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Easy); break;
                    case MidiTrackType.Pro_Keys_M: if (!parts.ProKeys[Difficulty.Medium] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Medium); break;
                    case MidiTrackType.Pro_Keys_H: if (!parts.ProKeys[Difficulty.Hard]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Hard); break;
                    case MidiTrackType.Pro_Keys_X: if (!parts.ProKeys[Difficulty.Expert] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Expert); break;

                    case MidiTrackType.Vocals: if (!parts.LeadVocals[0]    && Midi_Vocal_Preparser.Parse(track, true))  parts.LeadVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm1:  if (!parts.HarmonyVocals[0] && Midi_Vocal_Preparser.Parse(track, true))  parts.HarmonyVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm2:  if (!harm2) harm2 = Midi_Vocal_Preparser.Parse(track, false); break;
                    case MidiTrackType.Harm3:  if (!harm3) harm3 = Midi_Vocal_Preparser.Parse(track, false); break;
                }
            }

            // HARM 2/3 are not playable without HARM1 phrases
            if (parts.HarmonyVocals[0])
            {
                if (harm2)
                {
                    parts.HarmonyVocals.SetSubtrack(1);
                }
                if (harm3)
                {
                    parts.HarmonyVocals.SetSubtrack(2);
                }
            }
            return (ScanResult.Success, midiFile.Resolution);
        }

        protected static void SetDrums(ref AvailableParts parts, DrumPreparseHandler drumTracker)
        {
            if (drumTracker.Type == DrumsType.FiveLane)
            {
                parts.FiveLaneDrums.Difficulties = drumTracker.ValidatedDiffs;
            }
            else
            {
                parts.FourLaneDrums.Difficulties = drumTracker.ValidatedDiffs;
                if (drumTracker.Type == DrumsType.ProDrums)
                {
                    parts.ProDrums.Difficulties = drumTracker.ValidatedDiffs;
                }
            }
        }

        protected static bool CheckScanValidity(in AvailableParts parts)
        {
            return parts.FiveFretGuitar.SubTracks > 0 ||
                   parts.FiveFretBass.SubTracks > 0 ||
                   parts.FiveFretRhythm.SubTracks > 0 ||
                   parts.FiveFretCoopGuitar.SubTracks > 0 ||
                   parts.Keys.SubTracks > 0 ||

                   parts.SixFretGuitar.SubTracks > 0 ||
                   parts.SixFretBass.SubTracks > 0 ||
                   parts.SixFretRhythm.SubTracks > 0 ||
                   parts.SixFretCoopGuitar.SubTracks > 0 ||

                   parts.FourLaneDrums.SubTracks > 0 ||
                   parts.ProDrums.SubTracks > 0 ||
                   parts.FiveLaneDrums.SubTracks > 0 ||
                   parts.EliteDrums.SubTracks > 0 ||
                   parts.ProGuitar_17Fret.SubTracks > 0 ||
                   parts.ProGuitar_22Fret.SubTracks > 0 ||
                   parts.ProBass_17Fret.SubTracks > 0 ||
                   parts.ProBass_22Fret.SubTracks > 0 ||

                   parts.ProKeys.SubTracks > 0 ||

                   // parts.DJ.subTracks > 0 ||

                   parts.LeadVocals.SubTracks > 0 ||
                   parts.HarmonyVocals.SubTracks > 0;
        }
    }
}
