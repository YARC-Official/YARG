using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Song
{
    public static class SongExport
    {
        public static void ExportText(string path)
        {
            // TODO: Allow customizing sorting, as well as which metadata is written and in what order

            using var output = new StreamWriter(path);
            foreach (var (category, songs) in SongContainer.GetSortedCategory(SortAttribute.Artist))
            {
                output.WriteLine(category);
                output.WriteLine("--------------------");
                foreach (var song in songs)
                {
                    string artist = RichTextUtils.StripRichTextTags(song.Artist);
                    string name = RichTextUtils.StripRichTextTags(song.Name);
                    string playlist = RichTextUtils.StripRichTextTags(song.Playlist);

                    if (playlist == "Unknown Playlist")
                    {
                        output.WriteLine($"{artist} - {name}");
                    }
                    else
                    {
                        output.WriteLine($"{artist} - {name} from {playlist}");
                    }
                }

                output.WriteLine("");
            }

            output.Flush();
        }

        public static void ExportOuvert(string path)
        {
            OuvertExport.Export(path);
        }
    }
}