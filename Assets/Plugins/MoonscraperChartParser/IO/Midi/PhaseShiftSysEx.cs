// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using NAudio.Midi;

namespace MoonscraperChartEditor.Song.IO
{
    public class PhaseShiftSysEx : MidiEvent
    {
        public byte type;
        public byte difficulty;
        public byte code;
        public byte value;

        protected PhaseShiftSysEx()
        { }

        protected PhaseShiftSysEx(PhaseShiftSysEx other)
        {
            AbsoluteTime = other.AbsoluteTime;
            type = other.type;
            difficulty = other.difficulty;
            code = other.code;
            value = other.value;
        }

        public PhaseShiftSysEx(SysexEvent sysex) : base(sysex.AbsoluteTime, sysex.Channel, sysex.CommandCode)
        {
            if (!TryParseInternal(sysex, this))
            {
                throw new ArgumentException("The given event data is not a Phase Shift SysEx event.", nameof(sysex));
            }
        }

        public static bool TryParse(SysexEvent sysex, out PhaseShiftSysEx psSysex)
        {
            psSysex = new PhaseShiftSysEx();
            return TryParseInternal(sysex, psSysex);
        }

        protected static bool TryParseInternal(SysexEvent sysex, PhaseShiftSysEx psSysex)
        {
            byte[] sysexData = sysex.GetData();
            if (IsPhaseShiftSysex(sysexData))
            {
                psSysex.AbsoluteTime = sysex.AbsoluteTime;
                psSysex.type = sysexData[MidIOHelper.SYSEX_INDEX_TYPE];
                psSysex.difficulty = sysexData[MidIOHelper.SYSEX_INDEX_DIFFICULTY];
                psSysex.code = sysexData[MidIOHelper.SYSEX_INDEX_CODE];
                psSysex.value = sysexData[MidIOHelper.SYSEX_INDEX_VALUE];

                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsPhaseShiftSysex(byte[] sysexData)
        {
            if (sysexData == null)
                throw new ArgumentNullException(nameof(sysexData));

            return (
                sysexData.Length == MidIOHelper.SYSEX_LENGTH &&
                sysexData[MidIOHelper.SYSEX_INDEX_HEADER_1] == MidIOHelper.SYSEX_HEADER_1 &&
                sysexData[MidIOHelper.SYSEX_INDEX_HEADER_2] == MidIOHelper.SYSEX_HEADER_2 &&
                sysexData[MidIOHelper.SYSEX_INDEX_HEADER_3] == MidIOHelper.SYSEX_HEADER_3
            );
        }

        public bool MatchesWith(PhaseShiftSysEx otherEvent)
        {
            return type == otherEvent.type && difficulty == otherEvent.difficulty && code == otherEvent.code;
        }

        public override string ToString()
        {
            return $"AbsoluteTime: {AbsoluteTime}, Type: {type}, Difficulty: {difficulty}, Code: {code}, Value: {value}";
        }
    }

    public class PhaseShiftSysExStart : PhaseShiftSysEx
    {
        private PhaseShiftSysEx m_endEvent;
        public PhaseShiftSysEx endEvent
        {
            get => m_endEvent;
            set
            {
                if (value.AbsoluteTime < AbsoluteTime)
                    throw new ArgumentException($"The end event of a SysEx pair must occur after the start event.\nStart: {this}\nEnd: {value}", nameof(value));

                m_endEvent = value;
            }
        }

        public long Length
        {
            get
            {
                if (m_endEvent != null)
                {
                    return m_endEvent.AbsoluteTime - AbsoluteTime;
                }

                // No end event to get a length from
                return 0;
            }
        }

        public PhaseShiftSysExStart(PhaseShiftSysEx sysex) : base(sysex)
        { }

        public PhaseShiftSysExStart(PhaseShiftSysEx start, PhaseShiftSysEx end) : base(start)
        {
            if (start.AbsoluteTime < end.AbsoluteTime)
                throw new ArgumentException($"The start event of a SysEx pair must occur before the end event.\nStart: {start}\nEnd: {end}", nameof(start));

            m_endEvent = end;
        }

        public override string ToString()
        {
            if (m_endEvent != null)
                return $"AbsoluteTime: {AbsoluteTime}, Length: {Length}, Type: {type}, Difficulty: {difficulty}, Code: {code}, Value: {value}";
            else
                return base.ToString();
        }
    }
}
