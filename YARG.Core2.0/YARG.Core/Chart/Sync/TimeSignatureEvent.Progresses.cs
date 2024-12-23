using System;

namespace YARG.Core.Chart
{
    public partial class TimeSignatureChange
    {
        private void CheckTickStart(uint tick)
        {
            if (tick < Tick)
                throw new ArgumentOutOfRangeException($"The given tick ({tick}) must be greater than this event's tick ({Tick})!");
        }

        /// <summary>
        /// Calculates the fractional number of beats that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetBeatProgress(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / (double) GetTicksPerBeat(sync);
        }

        /// <summary>
        /// Calculates the whole number of beats that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetBeatCount(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / GetTicksPerBeat(sync);
        }

        /// <summary>
        /// Calculates the percent of a beat that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetBeatPercentage(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            uint tickRate = GetTicksPerBeat(sync);
            return (tick % tickRate) / (double) tickRate;
        }

        /// <summary>
        /// Calculates the fractional number of beats that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetBeatProgress(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetBeatProgress(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the whole number of beats that the given time lies at, relative to this time signature.
        /// </summary>
        public uint GetBeatCount(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetBeatCount(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the percent of a beat that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetBeatPercentage(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetBeatPercentage(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the fractional number of quarter notes that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNoteProgress(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / (double) GetTicksPerQuarterNote(sync);
        }

        /// <summary>
        /// Calculates the whole number of quarter notes that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetQuarterNoteCount(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / GetTicksPerQuarterNote(sync);
        }

        /// <summary>
        /// Calculates the percent of a quarter note that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNotePercentage(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            uint tickRate = GetTicksPerQuarterNote(sync);
            return (tick % tickRate) / (double) tickRate;
        }

        /// <summary>
        /// Calculates the fractional number of quarter notes that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNoteProgress(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetQuarterNoteProgress(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the whole number of quarter notes that the given time lies at, relative to this time signature.
        /// </summary>
        public uint GetQuarterNoteCount(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetQuarterNoteCount(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the percent of a quarter note that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetQuarterNotePercentage(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetQuarterNotePercentage(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the fractional number of measures that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetMeasureProgress(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / (double) GetTicksPerMeasure(sync);
        }

        /// <summary>
        /// Calculates the whole number of measures that the given tick lies at, relative to this time signature.
        /// </summary>
        public uint GetMeasureCount(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            return (tick - Tick) / GetTicksPerMeasure(sync);
        }

        /// <summary>
        /// Calculates the percent of a measure that the given tick lies at, relative to this time signature.
        /// </summary>
        public double GetMeasurePercentage(uint tick, SyncTrack sync)
        {
            CheckTickStart(tick);
            uint tickRate = GetTicksPerMeasure(sync);
            return (tick % tickRate) / (double) tickRate;
        }

        /// <summary>
        /// Calculates the fractional number of measures that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetMeasureProgress(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetMeasureProgress(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the whole number of measures that the given time lies at, relative to this time signature.
        /// </summary>
        public uint GetMeasureCount(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetMeasureCount(sync.TimeToTick(time, tempo), sync);
        }

        /// <summary>
        /// Calculates the percent of a measure that the given time lies at, relative to this time signature.
        /// </summary>
        public double GetMeasurePercentage(double time, SyncTrack sync, TempoChange tempo)
        {
            return GetMeasurePercentage(sync.TimeToTick(time, tempo), sync);
        }
    }
}