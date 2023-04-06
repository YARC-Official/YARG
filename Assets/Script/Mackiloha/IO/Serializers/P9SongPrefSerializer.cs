using Mackiloha.Song;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mackiloha.IO.Serializers
{
    public class P9SongPrefSerializer : AbstractSerializer
    {
        public P9SongPrefSerializer(MiloSerializer miloSerializer) : base(miloSerializer) { }

        public override void ReadFromStream(AwesomeReader ar, ISerializable data)
        {
            var songPref = data as P9SongPref;
            int version = ReadMagic(ar, data);

            // Skip meta for now
            ar.BaseStream.Position += 13;

            songPref.Venue = ar.ReadString();

            var count = ar.ReadInt32();
            songPref.MiniVenues.AddRange(RepeatFor(count, () => ar.ReadString()));

            count = ar.ReadInt32();
            songPref.Scenes.AddRange(RepeatFor(count, () => ar.ReadString()));

            var zero = ar.ReadInt32();
            if (zero != 0)
                throw new NotSupportedException();

            songPref.DreamscapeOutfit = ar.ReadString();
            songPref.StudioOutfit = ar.ReadString();

            count = ar.ReadInt32();
            songPref.GeorgeInstruments.AddRange(RepeatFor(count, () => ar.ReadString()));

            count = ar.ReadInt32();
            songPref.JohnInstruments.AddRange(RepeatFor(count, () => ar.ReadString()));

            count = ar.ReadInt32();
            songPref.PaulInstruments.AddRange(RepeatFor(count, () => ar.ReadString()));

            count = ar.ReadInt32();
            songPref.RingoInstruments.AddRange(RepeatFor(count, () => ar.ReadString()));

            songPref.Tempo = ar.ReadString();
            songPref.SongClips = ar.ReadString();
            songPref.DreamscapeFont = ar.ReadString();

            if (version <= 20) // TBRB
            {
                songPref.GeorgeAmp = ar.ReadString();
                songPref.JohnAmp = ar.ReadString();
                songPref.PaulAmp = ar.ReadString();
                songPref.Mixer = ar.ReadString();

                var enumValue = ar.ReadInt32();

                if (!Enum.IsDefined(typeof(DreamscapeCamera), enumValue))
                    throw new NotSupportedException($"Value of \'{enumValue}\' is not supported for dreamscape camera!");

                songPref.DreamscapeCamera = (DreamscapeCamera)enumValue;
            }
            else // GDRB
            {
                // Should always be 0
                zero = ar.ReadInt32();
                if (zero != 0)
                    throw new NotSupportedException();
            }

            songPref.LyricPart = ar.ReadString();

            if (version >= 25)
            {
                songPref.NormalOutfit = ar.ReadString();
                songPref.BonusOutfit = ar.ReadString();
                songPref.DrumSet = ar.ReadString();
                songPref.Era = ar.ReadString();

                songPref.CamDirectory = ar.ReadString();
                songPref.MediaDirectory = ar.ReadString();
                songPref.SongIntroCam = ar.ReadString();
                songPref.WinCam = ar.ReadString();
            }
        }

        public override void WriteToStream(AwesomeWriter aw, ISerializable data)
        {
            var songPref = data as P9SongPref;

            int version = string.IsNullOrEmpty(songPref.NormalOutfit) ? 20 : 25; // Super hacky but eh
            aw.Write((int)version);

            aw.Write((int)2); // Sub version?
            aw.BaseStream.Position += 9; // Skip other meta

            aw.Write(songPref.Venue);
            aw.Write(songPref.MiniVenues.Count);
            foreach (var venue in songPref.MiniVenues)
            {
                aw.Write((string)venue);
            }

            aw.Write(songPref.Scenes.Count);
            foreach (var scene in songPref.Scenes)
            {
                aw.Write((string)scene);
            }

            aw.Write((int)0); // Always 0

            aw.Write(songPref.DreamscapeOutfit);
            aw.Write(songPref.StudioOutfit);

            aw.Write(songPref.GeorgeInstruments.Count);
            foreach (var instrument in songPref.GeorgeInstruments)
            {
                aw.Write((string)instrument);
            }

            aw.Write(songPref.JohnInstruments.Count);
            foreach (var instrument in songPref.JohnInstruments)
            {
                aw.Write((string)instrument);
            }

            aw.Write(songPref.PaulInstruments.Count);
            foreach (var instrument in songPref.PaulInstruments)
            {
                aw.Write((string)instrument);
            }

            aw.Write(songPref.RingoInstruments.Count);
            foreach (var instrument in songPref.RingoInstruments)
            {
                aw.Write((string)instrument);
            }

            aw.Write(songPref.Tempo);
            aw.Write(songPref.SongClips);
            aw.Write(songPref.DreamscapeFont);

            if (version <= 20) // TBRB
            {
                aw.Write(songPref.GeorgeAmp);
                aw.Write(songPref.JohnAmp);
                aw.Write(songPref.PaulAmp);
                aw.Write(songPref.Mixer);

                aw.Write((int)songPref.DreamscapeCamera);
            }
            else // GDRB
            {
                aw.Write((int)0);
            }

            aw.Write(songPref.LyricPart);

            if (version >= 25)
            {
                aw.Write(songPref.NormalOutfit);
                aw.Write(songPref.BonusOutfit);
                aw.Write(songPref.DrumSet);
                aw.Write(songPref.Era);

                aw.Write(songPref.CamDirectory);
                aw.Write(songPref.MediaDirectory);
                aw.Write(songPref.SongIntroCam);
                aw.Write(songPref.WinCam);
            }
        }

        public override bool IsOfType(ISerializable data) => data is P9SongPref;

        public override int Magic()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 25:
                    // TBRB
                    // TODO: Refector to use optional different magic
                    return 20;
                default:
                    return -1;
            }
        }

        internal override int[] ValidMagics()
        {
            switch (MiloSerializer.Info.Version)
            {
                case 25:
                    // TBRB / GDRB
                    return new[] { 20, 25 };
                default:
                    return Array.Empty<int>();
            }
        }
    }
}
