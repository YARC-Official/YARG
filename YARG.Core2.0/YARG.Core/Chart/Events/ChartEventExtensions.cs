using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    public static class ChartEventExtensions
    {
        public static List<TNote> DuplicateNotes<TNote>(this List<TNote> notes)
            where TNote : Note<TNote>
        {
            int count = notes.Count;
            var newNotes = new List<TNote>(count);
            if (count < 1)
                return newNotes;

            // Clone first note separately so we can link everything together
            var previousNote = notes[0].Clone();
            newNotes.Add(previousNote);

            // Clone the rest
            for (int i = 1; i < notes.Count; i++)
            {
                var newNote = notes[i].Clone();

                // Assign forward/backward references
                newNote.PreviousNote = previousNote;
                foreach (var child in newNote.ChildNotes)
                    child.PreviousNote = previousNote;

                previousNote.NextNote = newNote;
                foreach (var child in previousNote.ChildNotes)
                    child.NextNote = newNote;

                // Add note to list
                newNotes.Add(newNote);
                previousNote = newNote;
            }

            return newNotes;
        }

        public static double GetStartTime<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Time;
        }

        public static double GetEndTime<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TimeEnd;
        }

        public static uint GetFirstTick<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[0];
            return chartEvent.Tick;
        }

        public static uint GetLastTick<TEvent>(this List<TEvent> events)
            where TEvent : ChartEvent
        {
            if (events.Count < 1)
                return 0;

            // Chart events are sorted
            var chartEvent = events[^1];
            return chartEvent.TickEnd;
        }

        public static TEvent? GetPrevious<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfPrevious(events, time);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent? GetPrevious<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfPrevious(events, tick);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent? GetNext<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfNext(events, time);
            if (index < 0)
                return null;

            return events[index];
        }

        public static TEvent? GetNext<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int index = GetIndexOfNext(events, tick);
            if (index < 0)
                return null;

            return events[index];
        }

        public static int GetIndexOfPrevious<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(time);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs before (or at) the given time
            while (closestIndex >= 0 && events[closestIndex].Time > time)
                closestIndex--;

            return closestIndex;
        }

        public static int GetIndexOfPrevious<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(tick);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs before (or at) the given tick
            while (closestIndex >= 0 && events[closestIndex].Tick > tick)
                closestIndex--;

            return closestIndex;
        }

        public static int GetIndexOfNext<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(time);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs after the given time
            int count = events.Count;
            while (closestIndex < count && events[closestIndex].Time <= time)
                closestIndex++;

            return closestIndex < count ? closestIndex : -1;
        }

        public static int GetIndexOfNext<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            int closestIndex = events.FindClosestEventIndex(tick);
            if (closestIndex < 0)
                return -1;

            // Ensure the index we return is for an event that occurs after the given tick
            int count = events.Count;
            while (closestIndex < count && events[closestIndex].Tick <= tick)
                closestIndex++;

            return closestIndex < count ? closestIndex : -1;
        }

        public static bool GetEventRange<TEvent>(this List<TEvent> events, double startTime, double endTime,
            out Range range)
            where TEvent : ChartEvent
        {
            range = default;

            int startIndex = events.FindClosestEventIndex(startTime);
            int endIndex = events.FindClosestEventIndex(endTime);
            if (startIndex < 0 || endIndex < 0)
                return false;

            // Ensure indexes are within time bounds
            int count = events.Count;
            while (startIndex < count && events[startIndex].Time < startTime)
                startIndex++;
            while (endIndex >= 0 && events[endIndex].Time >= endTime)
                endIndex--;

            // Ensure indexes are still in bounds
            if (startIndex >= count || endIndex < 0 || startIndex > endIndex)
                return false;

            range = new Range(startIndex, endIndex + 1);
            return true;
        }

        public static bool GetEventRange<TEvent>(this List<TEvent> events, uint startTick, uint endTick,
            out Range range)
            where TEvent : ChartEvent
        {
            range = default;

            int startIndex = events.FindClosestEventIndex(startTick);
            int endIndex = events.FindClosestEventIndex(endTick);
            if (startIndex < 0 || endIndex < 0)
                return false;

            // Ensure indexes are within tick bounds
            int count = events.Count;
            while (startIndex < count && events[startIndex].Tick < startTick)
                startIndex++;
            while (endIndex >= 0 && events[endIndex].Tick >= endTick)
                endIndex--;

            // Ensure indexes are still in bounds
            if (startIndex >= count || endIndex < 0 || startIndex > endIndex)
                return false;

            range = new Range(startIndex, endIndex + 1);
            return true;
        }

        public static TEvent? FindClosestEvent<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.BinarySearch(time, EventComparer<TEvent>.CompareTime);
        }

        public static TEvent? FindClosestEvent<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.BinarySearch(tick, EventComparer<TEvent>.CompareTick);
        }

        public static int FindClosestEventIndex<TEvent>(this List<TEvent> events, double time)
            where TEvent : ChartEvent
        {
            return events.BinarySearchIndex(time, EventComparer<TEvent>.CompareTime);
        }

        public static int FindClosestEventIndex<TEvent>(this List<TEvent> events, uint tick)
            where TEvent : ChartEvent
        {
            return events.BinarySearchIndex(tick, EventComparer<TEvent>.CompareTick);
        }

        private static class EventComparer<TEvent>
            where TEvent : ChartEvent
        {
            public static readonly Func<TEvent, double, int> CompareTime = _CompareTime;
            public static readonly Func<TEvent, uint, int> CompareTick = _CompareTick;

            private static int _CompareTime(TEvent currentEvent, double targetTime)
            {
                if (currentEvent.Time == targetTime)
                    return 0;
                else if (currentEvent.Time < targetTime)
                    return -1;
                else
                    return 1;
            }

            private static int _CompareTick(TEvent currentEvent, uint targetTick)
            {
                if (currentEvent.Tick == targetTick)
                    return 0;
                else if (currentEvent.Tick < targetTick)
                    return -1;
                else
                    return 1;
            }
        }
    }
}