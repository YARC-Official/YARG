using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Chart
{
    public class BeatHandler
    {
        private readonly MoonSong _song;

        public List<Beat> Beats;
        public List<Beat> Measures;

        public int TotalBeatCount => Beats.Count;
        private int lastMeasure;

        public int CurrentMeasure { get; private set; }

        public BeatHandler(MoonSong song)
        {
            _song = song;

            Beats = new List<Beat>();
            Measures = new List<Beat>();
        }

        private void AddBeat(uint tick, BeatStyle type)
        {
            Beats.Add(new Beat(tick, (float) _song.TickToTime(tick), type));

            if (type == BeatStyle.MEASURE) Measures.Add(Beats[^1]);
        }

        // Thanks mdsitton for the CH beat generation algorithm when I was making SideyBot :)
        public void GenerateBeats()
        {
            uint currentTick = 0;
            uint lastTick = _song.GetLastTick();

            // How many ticks to move forward to get to the next beat.
            uint forwardStep = 0;

            TimeSignature lastTS = null;
            TimeSignature currentTS;

            int currentBeatInMeasure = 0;
            int beatsInTS = 0;

            // The last beat style that was generated. Always starts as a measure.
            BeatStyle lastStyle = BeatStyle.MEASURE;

            // lastTick + forwardStep ensures we will always look ahead 1 more beat than the lastTick.
            while (currentTick < lastTick + forwardStep || lastStyle != BeatStyle.MEASURE)
            {
                // Gets previous time signature before currentTick.
                currentTS = _song.GetPrevTS(currentTick);

                // The number of "beats within a beat" there is
                uint currentSubBeat = currentTS.denominator / 4;

                bool hasTsChanged = lastTS == null || lastTS.numerator != currentTS.numerator ||
                    lastTS.denominator != currentTS.denominator;

                // If denominator is larger than 4 start off with weak beats, if 4 or less use strong beats
                var style = currentTS.denominator > 4 ? BeatStyle.WEAK : BeatStyle.STRONG;

                // New time signature. First beat of a new time sig is always a measure.
                if (hasTsChanged)
                {
                    beatsInTS = 0;
                    currentBeatInMeasure = 0;
                }

                // Beat count is equal to TS numerator, so its a new measure.
                if (currentBeatInMeasure == currentTS.numerator)
                {
                    currentBeatInMeasure = 0;
                }

                if (currentTS.denominator <= 4 || currentBeatInMeasure % currentSubBeat == 0)
                {
                    style = BeatStyle.STRONG;
                }

                // Make it a measure if first beat of a measure.
                if (currentBeatInMeasure == 0)
                {
                    style = BeatStyle.MEASURE;

                    // Handle 1/x TS's so that only the first beat in the TS gets a measure line
                    // and then from there it is marked at a strong beat every quarter note with everything else as weak
                    if (currentTS.numerator == 1 && beatsInTS > 0)
                    {
                        if (currentTick >= lastTick)
                        {
                            style = BeatStyle.MEASURE;
                        }
                        // if not quarter note length beats every quarter note is stressed
                        else if (currentTS.denominator <= 4 || (beatsInTS % currentSubBeat) == 0)
                        {
                            style = BeatStyle.STRONG;
                        }
                        else
                        {
                            style = BeatStyle.WEAK;
                        }
                    }
                }

                // Last beat of measure should never be a strong beat if denominator is bigger than 4.
                if (currentBeatInMeasure == currentTS.numerator - 1 && currentTS.denominator > 4 &&
                    currentTick < lastTick + forwardStep)
                {
                    style = BeatStyle.WEAK;
                }

                AddBeat(currentTick, style);

                currentBeatInMeasure++;
                beatsInTS++;

                forwardStep = (uint) (_song.resolution * 4) / currentTS.denominator;
                currentTick += forwardStep;
                lastTS = currentTS;
                lastStyle = style;
            }

            lastMeasure = Measures.Count - 1;
        }

        public void UpdateCurrentMeasure(double songTime)
        {
            while (CurrentMeasure <= lastMeasure && songTime > Measures[CurrentMeasure].Time)
            {
                CurrentMeasure++;
            }
        }

        public int GetNoteMeasure(Note note)
        {
            int dif = CurrentMeasure;

            while (dif < Measures.Count && note.Time >= Measures[dif].Time) dif++;

            return dif;
        }
    }

    public static class SongHelpers
    {
        public static uint GetLastTick(this MoonSong song)
        {
            uint lastTick = 0;
            foreach (var songEvent in song.events)
            {
                if (songEvent.tick > lastTick)
                {
                    lastTick = songEvent.tick;
                }
            }

            foreach (var chart in song.Charts)
            {
                foreach (var songObject in chart.chartObjects)
                {
                    if (songObject.tick <= lastTick) continue;

                    lastTick = songObject.tick;

                    if (songObject is MoonNote note)
                    {
                        lastTick += note.length;
                    }
                }
            }

            return lastTick;
        }
    }
}