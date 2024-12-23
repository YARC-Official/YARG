using Melanchall.DryWetMidi.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace MoonscraperChartEditor.Song.IO
{
    public static class MidFileLoader
    {
        public static MidiFile LoadMidiFile(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            return LoadMidiFile(stream);
        }

        public static MidiFile LoadMidiFile(Stream stream)
        {
            try
            {
                try
                {
                    YargLogger.LogTrace("Attempting to load midi in UTF-8");
                    return MidiFile.Read(stream, MidiSettings.Instance);

                }
                catch (DecoderFallbackException)
                {
                    stream.Position = 0;
                    YargLogger.LogTrace("Attempting to load midi in Latin-1");
                    return MidiFile.Read(stream, MidiSettingsLatin1.Instance);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Bad or corrupted midi file!", e);
            }
        }
    }
}
