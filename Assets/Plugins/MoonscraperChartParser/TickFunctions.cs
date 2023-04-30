// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;       // import math lib

namespace MoonscraperChartEditor.Song
{
    public static class TickFunctions
    {
        public const float SECONDS_PER_MINUTE = 60.0f;

        /// <summary>
        /// Calculates the amount of time elapsed between 2 tick positions.
        /// </summary>
        /// <param name="tickStart">Initial tick position.</param>
        /// <param name="tickEnd">Final tick position.</param>
        /// <param name="resolution">Ticks per beat, usually provided from the resolution song of a Song class.</param>
        /// <param name="bpm">The beats per minute value. BPMs provided from a BPM object need to be divded by 1000 as it is stored as the value read from a .chart file.</param>
        /// <returns></returns>
        public static double DisToTime(uint tickStart, uint tickEnd, float resolution, float bpm)
        {
            return (tickEnd - tickStart) / resolution * SECONDS_PER_MINUTE / bpm;
        }

        public static double DisToBpm(uint tickStart, uint tickEnd, double deltatime, double resolution)
        {
            return (tickEnd - tickStart) / resolution * SECONDS_PER_MINUTE / deltatime;
        }

        public static uint TimeToDis(double timeStart, double timeEnd, float resolution, float bpm)
        {
            return (uint)Math.Round((timeEnd - timeStart) * bpm / SECONDS_PER_MINUTE * resolution);
        }

        public static uint TickScaling(uint tick, float originalResolution, float outputResolution)
        {
            tick = (uint)Math.Round(tick * outputResolution / originalResolution);
            return tick;
        }
    }
}
