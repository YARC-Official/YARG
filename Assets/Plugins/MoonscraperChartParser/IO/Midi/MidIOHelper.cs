// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MoonscraperChartEditor.Song.IO
{
    public static class MidIOHelper
    {
        // Track names
        public const string BEAT_TRACK = "BEAT";
        public const string EVENTS_TRACK = "EVENTS";
        public const string VENUE_TRACK = "VENUE";
        public const string GUITAR_TRACK = "PART GUITAR";
        public const string GH1_GUITAR_TRACK = "T1 GEMS";
        public const string GUITAR_COOP_TRACK = "PART GUITAR COOP";
        public const string BASS_TRACK = "PART BASS";
        public const string RHYTHM_TRACK = "PART RHYTHM";
        public const string KEYS_TRACK = "PART KEYS";
        public const string DRUMS_TRACK = "PART DRUMS";
        public const string DRUMS_REAL_TRACK = "PART REAL_DRUMS_PS";
        public const string GHL_GUITAR_TRACK = "PART GUITAR GHL";
        public const string GHL_BASS_TRACK = "PART BASS GHL";
        public const string GHL_RHYTHM_TRACK = "PART RHYTHM GHL";
        public const string GHL_GUITAR_COOP_TRACK = "PART GUITAR COOP GHL";
        public const string VOCALS_TRACK = "PART VOCALS";

        // Note numbers
        public const byte DOUBLE_KICK_NOTE = 95;
        public const byte SOLO_NOTE = 103;                 // http://docs.c3universe.com/rbndocs/index.php?title=Guitar_and_Bass_Authoring#Solo_Sections
        public const byte TAP_NOTE_CH = 104;               // https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/5-Fret Guitar.md
        public const byte LYRICS_PHRASE_1 = 105;           // http://docs.c3universe.com/rbndocs/index.php?title=Vocal_Authoring
        public const byte LYRICS_PHRASE_2 = 106;           // Rock Band charts before RB3 mark phrases using this note as well
        public const byte FLAM_MARKER = 109;
        public const byte STARPOWER_NOTE = 116;            // http://docs.c3universe.com/rbndocs/index.php?title=Overdrive_and_Big_Rock_Endings

        // http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring#Drum_Fills
        public const byte STARPOWER_DRUM_FILL_0 = 120;
        public const byte STARPOWER_DRUM_FILL_1 = 121;
        public const byte STARPOWER_DRUM_FILL_2 = 122;
        public const byte STARPOWER_DRUM_FILL_3 = 123;
        public const byte STARPOWER_DRUM_FILL_4 = 124;

        // Drum rolls - http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring#Drum_Rolls
        public const byte DRUM_ROLL_STANDARD = 126;
        public const byte DRUM_ROLL_SPECIAL = 127;

        // Text events
        public const string SOLO_EVENT_TEXT = "solo";
        public const string SOLO_END_EVENT_TEXT = "soloend";

        public const string LYRIC_EVENT_PREFIX = LyricHelper.LYRIC_EVENT_PREFIX;
        public const string LYRICS_PHRASE_START_TEXT = LyricHelper.PhraseStartText;
        public const string LYRICS_PHRASE_END_TEXT = LyricHelper.PhraseEndText;

        public const string SECTION_PREFIX_RB2 = "section ";
        public const string SECTION_PREFIX_RB3 = "prc_";

        // These events are valid both with and without brackets.
        // The bracketed versions follow the style of other existing .mid text events.
        public const string CHART_DYNAMICS_TEXT = "ENABLE_CHART_DYNAMICS";
        public const string CHART_DYNAMICS_TEXT_BRACKET = "[ENABLE_CHART_DYNAMICS]";
        public const string ENHANCED_OPENS_TEXT = "ENHANCED_OPENS";
        public const string ENHANCED_OPENS_TEXT_BRACKET = "[ENHANCED_OPENS]";

        // Note velocities
        public const byte VELOCITY = 100;             // default note velocity for exporting
        public const byte VELOCITY_ACCENT = 127;      // fof/ps
        public const byte VELOCITY_GHOST = 1;         // fof/ps

        // Lookup tables
        public static readonly IReadOnlyDictionary<MoonSong.Difficulty, int> GUITAR_DIFF_START_LOOKUP = new Dictionary<MoonSong.Difficulty, int>()
        {
            { MoonSong.Difficulty.Easy, 60 },
            { MoonSong.Difficulty.Medium, 72 },
            { MoonSong.Difficulty.Hard, 84 },
            { MoonSong.Difficulty.Expert, 96 }
        };

        public static readonly IReadOnlyDictionary<MoonSong.Difficulty, int> GHL_GUITAR_DIFF_START_LOOKUP = new Dictionary<MoonSong.Difficulty, int>()
        {
            { MoonSong.Difficulty.Easy, 58 },
            { MoonSong.Difficulty.Medium, 70 },
            { MoonSong.Difficulty.Hard, 82 },
            { MoonSong.Difficulty.Expert, 94 }
        };

        public static readonly IReadOnlyDictionary<MoonSong.Difficulty, int> DRUMS_DIFF_START_LOOKUP = new Dictionary<MoonSong.Difficulty, int>()
        {
            { MoonSong.Difficulty.Easy, 60 },
            { MoonSong.Difficulty.Medium, 72 },
            { MoonSong.Difficulty.Hard, 84 },
            { MoonSong.Difficulty.Expert, 96 }
        };

        // http://docs.c3universe.com/rbndocs/index.php?title=Drum_Authoring
        public static readonly IReadOnlyDictionary<MoonNote.DrumPad, int> PAD_TO_CYMBAL_LOOKUP = new Dictionary<MoonNote.DrumPad, int>()
        {
            { MoonNote.DrumPad.Yellow, 110 },
            { MoonNote.DrumPad.Blue, 111 },
            { MoonNote.DrumPad.Orange, 112 },
        };

        public static readonly IReadOnlyDictionary<int, MoonNote.DrumPad> CYMBAL_TO_PAD_LOOKUP = PAD_TO_CYMBAL_LOOKUP.ToDictionary((i) => i.Value, (i) => i.Key);

        // SysEx event format
        // https://dwsk.proboards.com/thread/404/song-standard-advancements

        // Data as given by NAudio:
        // sysexData[0]: SysEx event length
        // sysexData[1-3]: Header
        // sysexData[4]: Event type
        // sysexData[5]: Difficulty
        // sysexData[6]: Event code
        // sysexData[7]: Event value

        // This length and the following indicies may need to be adjusted if the MIDI parsing library ever changes.
        // They both account for the length value that NAudio provides as the first value.
        public const int SYSEX_LENGTH = 8;

        public const int SYSEX_INDEX_HEADER_1 = 1;
        public const int SYSEX_INDEX_HEADER_2 = 2;
        public const int SYSEX_INDEX_HEADER_3 = 3;
        public const int SYSEX_INDEX_TYPE = 4;
        public const int SYSEX_INDEX_DIFFICULTY = 5;
        public const int SYSEX_INDEX_CODE = 6;
        public const int SYSEX_INDEX_VALUE = 7;

        public const char SYSEX_HEADER_1 = 'P'; // 0x50
        public const char SYSEX_HEADER_2 = 'S'; // 0x53
        public const char SYSEX_HEADER_3 = '\0'; // 0x00

        public const byte SYSEX_TYPE_PHRASE = 0x00;

        public const byte SYSEX_DIFFICULTY_EASY = 0x00;
        public const byte SYSEX_DIFFICULTY_MEDIUM = 0x01;
        public const byte SYSEX_DIFFICULTY_HARD = 0x02;
        public const byte SYSEX_DIFFICULTY_EXPERT = 0x03;
        public const byte SYSEX_DIFFICULTY_ALL = 0xFF;

        public static readonly Dictionary<byte, MoonSong.Difficulty> SYSEX_TO_MS_DIFF_LOOKUP = new Dictionary<byte, MoonSong.Difficulty>()
        {
            { SYSEX_DIFFICULTY_EASY, MoonSong.Difficulty.Easy },
            { SYSEX_DIFFICULTY_MEDIUM, MoonSong.Difficulty.Medium },
            { SYSEX_DIFFICULTY_HARD, MoonSong.Difficulty.Hard },
            { SYSEX_DIFFICULTY_EXPERT, MoonSong.Difficulty.Expert }
        };

        public static readonly Dictionary<MoonSong.Difficulty, byte> MS_TO_SYSEX_DIFF_LOOKUP = SYSEX_TO_MS_DIFF_LOOKUP.ToDictionary((i) => i.Value, (i) => i.Key);

        public const byte SYSEX_CODE_GUITAR_OPEN = 0x01;
        public const byte SYSEX_CODE_GUITAR_TAP = 0x04;

        // These codes aren't used by us, they're here for informational/future purposes
        public const byte SYSEX_CODE_REAL_DRUMS_HIHAT_OPEN = 0x05;
        public const byte SYSEX_CODE_REAL_DRUMS_HIHAT_PEDAL = 0x06;
        public const byte SYSEX_CODE_REAL_DRUMS_SNARE_RIMSHOT = 0x07;
        public const byte SYSEX_CODE_REAL_DRUMS_HIHAT_SIZZLE = 0x08;
        public const byte SYSEX_CODE_REAL_DRUMS_CYMBAL_AND_TOM_YELLOW = 0x11;
        public const byte SYSEX_CODE_REAL_DRUMS_CYMBAL_AND_TOM_BLUE = 0x12;
        public const byte SYSEX_CODE_REAL_DRUMS_CYMBAL_AND_TOM_GREEN = 0x13;
        public const byte SYSEX_CODE_PRO_GUITAR_SLIDE_UP = 0x02;
        public const byte SYSEX_CODE_PRO_GUITAR_SLIDE_DOWN = 0x03;
        public const byte SYSEX_CODE_PRO_GUITAR_PALM_MUTE = 0x09;
        public const byte SYSEX_CODE_PRO_GUITAR_VIBRATO = 0x0A;
        public const byte SYSEX_CODE_PRO_GUITAR_HARMONIC = 0x0B;
        public const byte SYSEX_CODE_PRO_GUITAR_PINCH_HARMONIC = 0x0C;
        public const byte SYSEX_CODE_PRO_GUITAR_BEND = 0x0D;
        public const byte SYSEX_CODE_PRO_GUITAR_ACCENT = 0x0E;
        public const byte SYSEX_CODE_PRO_GUITAR_POP = 0x0F;
        public const byte SYSEX_CODE_PRO_GUITAR_SLAP = 0x10;

        public const byte SYSEX_VALUE_PHRASE_START = 0x01;
        public const byte SYSEX_VALUE_PHRASE_END = 0x00;
    }
}
