using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Helpers;

namespace YARG.Song
{
    public static class SongExport
    {
        public enum ExportFormat
        {
            Json,
            Text,
            Csv
        }

        public static void Export(ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Json:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "json", ExportJson);
                    break;
                case ExportFormat.Text:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "txt", ExportText);
                    break;
                case ExportFormat.Csv:
                    FileExplorerHelper.OpenSaveFile(null, "songs", "csv", ExportCsv);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private static void ExportText(string path)
        {
            using var output = new StreamWriter(path);
            foreach (var (category, songs) in SongContainer.GetSortedCategory(SortAttribute.Artist))
            {
                output.WriteLine(category);
                output.WriteLine("--------------------");
                foreach (var song in songs)
                {
                    string artist = RichTextUtils.StripRichTextTags(song.Artist);
                    string name = RichTextUtils.StripRichTextTags(song.Name);
                    output.WriteLine($"{artist} - {name}");
                }

                output.WriteLine("");
            }

            output.Flush();
        }

        private static void ExportJson(string path)
        {
            OuvertExport.Export(path);
        }

        private static void ExportCsv(string path)
        {
            using var output = new StreamWriter(path);
            
            output.WriteLine(
                "Name,Artist,Album,Genre,Year,Length,Charter,Playlist,Source," +
                "Master,Age Rating,Vocal Parts," +
                "Guitar (5-Fret),Bass (5-Fret),Rhythm (5-Fret),Co-op (5-Fret),Keys," +
                "Guitar (6-Fret),Bass (6-Fret),Rhythm (6-Fret),Co-op (6-Fret)," +
                "Drums (4-Lane),Pro Drums,Drums (5-Lane),Elite Drums," +
                "Pro Guitar (17-Fret),Pro Guitar (22-Fret),Pro Bass (17-Fret),Pro Bass (22-Fret),Pro Keys," +
                "Vocals,Harmony,Band,Format,Hash"
            );

            foreach (var song in SongContainer.Songs)
            {
                string name = Escape(RichTextUtils.StripRichTextTags(song.Name));
                string artist = Escape(RichTextUtils.StripRichTextTags(song.Artist));
                string album = Escape(RichTextUtils.StripRichTextTags(song.Album));
                string genre = Escape(RichTextUtils.StripRichTextTags(song.Genre));
                string year = Escape(RichTextUtils.StripRichTextTags(song.UnmodifiedYear));
                string charter = Escape(RichTextUtils.StripRichTextTags(song.Charter));
                string playlist = Escape(RichTextUtils.StripRichTextTags(song.Playlist));
                string source = Escape(RichTextUtils.StripRichTextTags(song.Source));

                int totalSeconds = (int) song.SongLengthSeconds;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                string songLength = $"{minutes}:{seconds:D2}";

                string songRating = song.SongRating switch
                {
                    SongRating.Family_Friendly => "Family Friendly",
                    SongRating.Supervision_Recommended => "Supervision Recommended",
                    SongRating.Mature => "Mature",
                    _ => "No Rating"
                };

                bool isMaster = song.IsMaster;
                int vocalsCount = song.VocalsCount;

                int fiveFretGuitar = song.HasInstrument(Instrument.FiveFretGuitar) ? song[Instrument.FiveFretGuitar].Intensity : -1;
                int fiveFretBass = song.HasInstrument(Instrument.FiveFretBass) ? song[Instrument.FiveFretBass].Intensity : -1;
                int fiveFretRhythm = song.HasInstrument(Instrument.FiveFretRhythm) ? song[Instrument.FiveFretRhythm].Intensity : -1;
                int fiveFretCoopGuitar = song.HasInstrument(Instrument.FiveFretCoopGuitar) ? song[Instrument.FiveFretCoopGuitar].Intensity : -1;
                int keys = song.HasInstrument(Instrument.Keys) ? song[Instrument.Keys].Intensity : -1;
                int sixFretGuitar = song.HasInstrument(Instrument.SixFretGuitar) ? song[Instrument.SixFretGuitar].Intensity : -1;
                int sixFretBass = song.HasInstrument(Instrument.SixFretBass) ? song[Instrument.SixFretBass].Intensity : -1;
                int sixFretRhythm = song.HasInstrument(Instrument.SixFretRhythm) ? song[Instrument.SixFretRhythm].Intensity : -1;
                int sixFretCoopGuitar = song.HasInstrument(Instrument.SixFretCoopGuitar) ? song[Instrument.SixFretCoopGuitar].Intensity : -1;
                int fourLaneDrums = song.HasInstrument(Instrument.FourLaneDrums) ? song[Instrument.FourLaneDrums].Intensity : -1;
                int proDrums = song.HasInstrument(Instrument.ProDrums) ? song[Instrument.ProDrums].Intensity : -1;
                int fiveLaneDrums = song.HasInstrument(Instrument.FiveLaneDrums) ? song[Instrument.FiveLaneDrums].Intensity : -1;
                int eliteDrums = song.HasInstrument(Instrument.EliteDrums) ? song[Instrument.EliteDrums].Intensity : -1;
                int proGuitar17 = song.HasInstrument(Instrument.ProGuitar_17Fret) ? song[Instrument.ProGuitar_17Fret].Intensity : -1;
                int proGuitar22 = song.HasInstrument(Instrument.ProGuitar_22Fret) ? song[Instrument.ProGuitar_22Fret].Intensity : -1;
                int proBass17 = song.HasInstrument(Instrument.ProBass_17Fret) ? song[Instrument.ProBass_17Fret].Intensity : -1;
                int proBass22 = song.HasInstrument(Instrument.ProBass_22Fret) ? song[Instrument.ProBass_22Fret].Intensity : -1;
                int proKeys = song.HasInstrument(Instrument.ProKeys) ? song[Instrument.ProKeys].Intensity : -1;
                int vocals = song.HasInstrument(Instrument.Vocals) ? song[Instrument.Vocals].Intensity : -1;
                int harmony = song.HasInstrument(Instrument.Harmony) ? song[Instrument.Harmony].Intensity : -1;
                int band = song.BandDifficulty;

                string subType = song.SubType.ToString();
                string hash = song.Hash.ToString();

                output.WriteLine(
                    $"{name},{artist},{album},{genre},{year},{songLength},{charter},{playlist},{source}," +
                    $"{isMaster},{songRating},{vocalsCount}," +
                    $"{fiveFretGuitar},{fiveFretBass},{fiveFretRhythm},{fiveFretCoopGuitar},{keys}," +
                    $"{sixFretGuitar},{sixFretBass},{sixFretRhythm},{sixFretCoopGuitar}," +
                    $"{fourLaneDrums},{proDrums},{fiveLaneDrums},{eliteDrums}," +
                    $"{proGuitar17},{proGuitar22},{proBass17},{proBass22},{proKeys}," +
                    $"{vocals},{harmony},{band},{subType},{hash}"
                );
            }

            output.Flush();

            string Escape(string field)
            {
                const string quote = "\"";
                const string escapedQuote = "\"\"";

                if (string.IsNullOrEmpty(field))
                {
                    return "";
                }

                bool needsEscaping = field.Contains(',')
                    || field.Contains('"')
                    || field.Contains('\n')
                    || field.Contains('\r');

                if (needsEscaping)
                {
                    string escaped = field.Replace(quote, escapedQuote);
                    return $"{quote}{escaped}{quote}";
                }

                return field;
            }
        }
    }
}
