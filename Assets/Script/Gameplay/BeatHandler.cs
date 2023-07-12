using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public class BeatHandler
    {
        private readonly SongChart _song;

        public List<Beat> Beats;
        public List<Beat> Measures;

        public int TotalBeatCount => Beats.Count;
        private int lastMeasure;

        public int CurrentMeasure { get; private set; }

        public BeatHandler(SongChart song)
        {
            _song = song;

            Beats = new List<Beat>();
            Measures = new List<Beat>();
        }

        private void AddBeat(uint tick, BeatStyle type)
        {
            Beats.Add(new Beat(tick, (float) _song.TickToTime(tick), type));

            if (type == BeatStyle.Measure) Measures.Add(Beats[^1]);
        }

        // Thanks mdsitton for the CH beat generation algorithm when I was making SideyBot :)
        public void GenerateBeats()
        {
            uint currentTick = 0;
            uint lastTick = _song.GetLastTick();

            // How many ticks to move forward to get to the next beat.
            uint forwardStep = 0;

            TimeSignatureChange lastTS = null;
            TimeSignatureChange currentTS;

            int currentBeatInMeasure = 0;
            int beatsInTS = 0;

            // The last beat style that was generated. Always starts as a measure.
            BeatStyle lastStyle = BeatStyle.Measure;

            // lastTick + forwardStep ensures we will always look ahead 1 more beat than the lastTick.
            while (currentTick < lastTick + forwardStep || lastStyle != BeatStyle.Measure)
            {
                // Gets previous time signature before currentTick.
                currentTS = _song.SyncTrack.GetPrevTimeSignature(currentTick);

                // The number of "beats within a beat" there is
                uint currentSubBeat = currentTS.Denominator / 4;

                bool hasTsChanged = lastTS == null || lastTS.Numerator != currentTS.Numerator ||
                    lastTS.Denominator != currentTS.Denominator;

                // If denominator is larger than 4 start off with weak beats, if 4 or less use strong beats
                var style = currentTS.Denominator > 4 ? BeatStyle.Weak : BeatStyle.Strong;

                // New time signature. First beat of a new time sig is always a measure.
                if (hasTsChanged)
                {
                    beatsInTS = 0;
                    currentBeatInMeasure = 0;
                }

                // Beat count is equal to TS numerator, so its a new measure.
                if (currentBeatInMeasure == currentTS.Numerator)
                {
                    currentBeatInMeasure = 0;
                }

                if (currentTS.Denominator <= 4 || currentBeatInMeasure % currentSubBeat == 0)
                {
                    style = BeatStyle.Strong;
                }

                // Make it a measure if first beat of a measure.
                if (currentBeatInMeasure == 0)
                {
                    style = BeatStyle.Measure;

                    // Handle 1/x TS's so that only the first beat in the TS gets a measure line
                    // and then from there it is marked at a strong beat every quarter note with everything else as weak
                    if (currentTS.Numerator == 1 && beatsInTS > 0)
                    {
                        if (currentTick >= lastTick)
                        {
                            style = BeatStyle.Measure;
                        }
                        // if not quarter note length beats every quarter note is stressed
                        else if (currentTS.Denominator <= 4 || (beatsInTS % currentSubBeat) == 0)
                        {
                            style = BeatStyle.Strong;
                        }
                        else
                        {
                            style = BeatStyle.Weak;
                        }
                    }
                }

                // Last beat of measure should never be a strong beat if denominator is bigger than 4.
                if (currentBeatInMeasure == currentTS.Numerator - 1 && currentTS.Denominator > 4 &&
                    currentTick < lastTick + forwardStep)
                {
                    style = BeatStyle.Weak;
                }

                AddBeat(currentTick, style);

                currentBeatInMeasure++;
                beatsInTS++;

                forwardStep = _song.Resolution * 4 / currentTS.Denominator;
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

        public int GetEventMeasure(ChartEvent note)
        {
            int dif = CurrentMeasure;

            while (dif < Measures.Count && note.Time >= Measures[dif].Time) dif++;

            return dif;
        }
    }
}