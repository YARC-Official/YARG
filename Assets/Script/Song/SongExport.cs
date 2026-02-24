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
                "Name,Artist,Album,Genre,Subgenre,Year,Length," +
                "Charter,Playlist,Source,Master,Age Rating,Vocal Parts," +
                "Guitar (5-Fret) Difficulty,Bass (5-Fret) Difficulty,Rhythm (5-Fret) Difficulty,Co-op (5-Fret) Difficulty,Keys Difficulty," +
                "Guitar (6-Fret) Difficulty,Bass (6-Fret) Difficulty,Rhythm (6-Fret) Difficulty,Co-op (6-Fret) Difficulty," +
                "Drums (4-Lane) Difficulty,Pro Drums Difficulty,Drums (5-Lane) Difficulty,Elite Drums Difficulty," +
                "Pro Guitar (17-Fret) Difficulty,Pro Guitar (22-Fret) Difficulty,Pro Bass (17-Fret) Difficulty,Pro Bass (22-Fret) Difficulty,Pro Keys Difficulty," +
                "Vocals Difficulty,Harmony Difficulty,Band Difficulty,Format,Hash"
            );

            foreach (var song in SongContainer.Songs)
            {
                string name = Escape(RichTextUtils.StripRichTextTags(song.Name));
                string artist = Escape(RichTextUtils.StripRichTextTags(song.Artist));
                string album = Escape(RichTextUtils.StripRichTextTags(song.Album));
                string genre = Escape(RichTextUtils.StripRichTextTags(song.Genre));
                string subgenre = Escape(RichTextUtils.StripRichTextTags(song.Subgenre));
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
                    SongRating.Family_Friendly         => "Family Friendly",
                    SongRating.Supervision_Recommended => "Supervision Recommended",
                    SongRating.Mature                  => "Mature",
                    _                                  => "No Rating"
                };

                bool isMaster = song.IsMaster;
                int vocalsCount = song.VocalsCount;

                int GetIntensity(Instrument inst)
                {
                    if (song.HasInstrument(inst))
                    {
                        return Math.Max((sbyte) 0, song[inst].Intensity);
                    }
                    return -1;
                }

                int fiveFretGuitar = GetIntensity(Instrument.FiveFretGuitar);
                int fiveFretBass = GetIntensity(Instrument.FiveFretBass);
                int fiveFretRhythm = GetIntensity(Instrument.FiveFretRhythm);
                int fiveFretCoopGuitar = GetIntensity(Instrument.FiveFretCoopGuitar);
                int keys = GetIntensity(Instrument.Keys);
                int sixFretGuitar = GetIntensity(Instrument.SixFretGuitar);
                int sixFretBass = GetIntensity(Instrument.SixFretBass);
                int sixFretRhythm = GetIntensity(Instrument.SixFretRhythm);
                int sixFretCoopGuitar = GetIntensity(Instrument.SixFretCoopGuitar);
                int fourLaneDrums = GetIntensity(Instrument.FourLaneDrums);
                int proDrums = GetIntensity(Instrument.ProDrums);
                int fiveLaneDrums = GetIntensity(Instrument.FiveLaneDrums);
                int eliteDrums = GetIntensity(Instrument.EliteDrums);
                int proGuitar17 = GetIntensity(Instrument.ProGuitar_17Fret);
                int proGuitar22 = GetIntensity(Instrument.ProGuitar_22Fret);
                int proBass17 = GetIntensity(Instrument.ProBass_17Fret);
                int proBass22 = GetIntensity(Instrument.ProBass_22Fret);
                int proKeys = GetIntensity(Instrument.ProKeys);
                int vocals = GetIntensity(Instrument.Vocals);
                int harmony = GetIntensity(Instrument.Harmony);
                int band = song.BandDifficulty;

                string subType = song.SubType.ToString();
                string hash = song.Hash.ToString();

                output.WriteLine(
                    $"{name},{artist},{album},{genre},{subgenre},{year},{songLength}," +
                    $"{charter},{playlist},{source},{isMaster},{songRating},{vocalsCount}," +
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
