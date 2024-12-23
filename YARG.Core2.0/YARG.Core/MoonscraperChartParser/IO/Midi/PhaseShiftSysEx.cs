// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;

namespace MoonscraperChartEditor.Song.IO
{
    // SysEx event format: https://dwsk.proboards.com/thread/404/song-standard-advancements
    internal class PhaseShiftSysEx : SysExEvent
    {
        public enum Type : byte
        {
            Phrase = 0x00
        }

        public enum Difficulty : byte
        {
            Easy = 0x00,
            Medium = 0x01,
            Hard = 0x02,
            Expert = 0x03,

            All = 0xFF
        }

        public enum PhraseCode : byte
        {
            Guitar_Open = 0x01,
            Guitar_Tap = 0x04,

            RealDrums_HihatOpen = 0x05,
            RealDrums_HihatPedal = 0x06,
            RealDrums_SnareRimshot = 0x07,
            RealDrums_HihatSizzle = 0x08,
            RealDrums_CymbalAndTomYellow = 0x11,
            RealDrums_CymbalAndTomBlue = 0x12,
            RealDrums_CymbalAndTomGreen = 0x13,

            ProGuitar_SlideUp = 0x02,
            ProGuitar_SlideDown = 0x03,
            ProGuitar_PalmMute = 0x09,
            ProGuitar_Vibrato = 0x0A,
            ProGuitar_Harmonic = 0x0B,
            ProGuitar_PinchHarmonic = 0x0C,
            ProGuitar_Bend = 0x0D,
            ProGuitar_Accent = 0x0E,
            ProGuitar_Pop = 0x0F,
            ProGuitar_Slap = 0x10,
        }

        public enum PhraseValue : byte
        {
            End = 0x00,
            Start = 0x01,
        }

        // Data as provided by DryWetMidi:
        // sysexData[0-2]: Header
        // sysexData[3]: Event type
        // sysexData[4]: Difficulty
        // sysexData[5]: Event code
        // sysexData[6]: Event value
        // sysexData[7]: SysEx terminator (F7)

        private const int EXPECTED_LENGTH = 8;

        private const int INDEX_TYPE = 3;
        private const int INDEX_DIFFICULTY = 4;
        private const int INDEX_CODE = 5;
        private const int INDEX_VALUE = 6;

        private static readonly Range HEADER_RANGE = ..3;
        private static readonly byte[] HEADER = { 0x50, 0x53, 0x00 }; // "PS\0"

        public static readonly Dictionary<Difficulty, MoonSong.Difficulty> SysExDiffToMsDiff = new()
        {
            { Difficulty.Easy, MoonSong.Difficulty.Easy },
            { Difficulty.Medium, MoonSong.Difficulty.Medium },
            { Difficulty.Hard, MoonSong.Difficulty.Hard },
            { Difficulty.Expert, MoonSong.Difficulty.Expert }
        };

        public static readonly Dictionary<MoonSong.Difficulty, Difficulty> MsDiffToSysExDiff =
            SysExDiffToMsDiff.ToDictionary((i) => i.Value, (i) => i.Key);

        private byte m_type;
        private byte m_difficulty;
        public byte code;
        public byte value;

        public Type type
        {
            get => (Type)m_type;
            set => m_type = (byte)value;
        }

        public Difficulty difficulty
        {
            get => (Difficulty)m_difficulty;
            set => m_difficulty = (byte)value;
        }

        public PhraseCode phraseCode
        {
            get => (PhraseCode)code;
            set => code = (byte)value;
        }

        public PhraseValue phraseValue
        {
            get => (PhraseValue)value;
            set => this.value = (byte)value;
        }

        protected PhaseShiftSysEx() : base(MidiEventType.NormalSysEx)
        { }

        protected PhaseShiftSysEx(PhaseShiftSysEx other) : this()
        {
            DeltaTime = other.DeltaTime;
            m_type = other.m_type;
            m_difficulty = other.m_difficulty;
            code = other.code;
            value = other.value;
        }

        public PhaseShiftSysEx(SysExEvent sysex) : this()
        {
            if (!TryParseInternal(sysex, this))
            {
                throw new ArgumentException("The given event data is not a Phase Shift SysEx event.", nameof(sysex));
            }
        }

        public static bool TryParse(SysExEvent sysex, out PhaseShiftSysEx psSysex)
        {
            psSysex = new PhaseShiftSysEx();
            return TryParseInternal(sysex, psSysex);
        }

        protected static bool TryParseInternal(SysExEvent sysex, PhaseShiftSysEx psSysex)
        {
            byte[] sysexData = sysex.Data;
            if (IsPhaseShiftSysex(sysexData))
            {
                psSysex.DeltaTime = sysex.DeltaTime;
                psSysex.m_type = sysexData[INDEX_TYPE];
                psSysex.m_difficulty = sysexData[INDEX_DIFFICULTY];
                psSysex.code = sysexData[INDEX_CODE];
                psSysex.value = sysexData[INDEX_VALUE];

                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsPhaseShiftSysex(ReadOnlySpan<byte> sysexData)
        {
            if (sysexData == null)
                throw new ArgumentNullException(nameof(sysexData));

            return sysexData.Length == EXPECTED_LENGTH && sysexData[HEADER_RANGE].SequenceEqual(HEADER);
        }

        public bool MatchesWith(PhaseShiftSysEx otherEvent)
        {
            return type == otherEvent.type && difficulty == otherEvent.difficulty && code == otherEvent.code;
        }

        public override string ToString()
        {
            return $"DeltaTime: {DeltaTime}, Type: {type}, Difficulty: {difficulty}, Code: {code}, Value: {value}";
        }

        protected override MidiEvent CloneEvent()
        {
            return new PhaseShiftSysEx(this);
        }
    }
}
