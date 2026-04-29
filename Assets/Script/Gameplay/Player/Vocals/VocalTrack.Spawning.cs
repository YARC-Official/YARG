using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public partial class VocalTrack
    {
        private int[] _phraseMarkerIndices;

        // Static vocals-related constants
        private const float STATIC_LYRICS_SPACING_FROM_SING_LINE = .25f;
        private const float STATIC_LYRICS_LEFT_EDGE = VocalElement.SING_LINE_POS + STATIC_LYRICS_SPACING_FROM_SING_LINE;
        private const float DEFAULT_STATIC_LYRICS_RIGHT_EDGE = STATIC_LYRICS_LEFT_EDGE + VocalLyricContainer.STATIC_PHRASE_SPACING;
        private const int MAXIMUM_STATIC_PHRASE_QUEUE_SIZE = 10;
        private const float STATIC_LYRIC_SHIFT_DURATION = .1f;
        private const int SCROLLING_LYRIC_SPAWN_BUDGET = 4;
        private const int STATIC_PHRASE_ENQUEUE_BUDGET = 2;

        private ScrollingPhraseNoteTracker[] _scrollingNoteTrackers;
        private ScrollingPhraseNoteTracker[] _scrollingLyricTrackers;
        private StaticPhraseTracker[] _staticPhraseTrackers;
        private Queue<VocalStaticLyricPhraseElement>[] _staticPhraseQueues;

        private Dictionary<LyricEvent, VocalScrollingLyricSyllableElement.PreparedLyric> _preparedScrollingLyrics;
        private List<VocalStaticLyricPhraseElement.PreparedPhrase>[] _preparedStaticPhrases;

        private int[] _highestEnqueuedPhrasePairIndices = { -1, -1, -1 };
        private float[] _rightEdges = {
            DEFAULT_STATIC_LYRICS_RIGHT_EDGE,
            DEFAULT_STATIC_LYRICS_RIGHT_EDGE,
            DEFAULT_STATIC_LYRICS_RIGHT_EDGE
        };

        private bool[] _noMoreStaticPhrases = { false, false, false };

        private void UpdateSpawning()
        {
            // For each harmony...
            for (int i = 0; i < _vocalsTrack.Parts.Count; i++)
            {
                // Spawn in notes and lyrics
                SpawnNotesInPhrase(_scrollingNoteTrackers[i], i);
                SpawnPhraseLines(i);

                if (!SettingsManager.Settings.StaticVocalsMode.Value)
                {
                    SpawnScrollingLyrics(_scrollingLyricTrackers[i], i);
                }
            }

            if (SettingsManager.Settings.StaticVocalsMode.Value)
            {
                for (int i = 0; i < LyricLaneCount; i++)
                {
                    SpawnStaticLyrics(_staticPhraseTrackers[i], i);
                }
            }
        }

        private void SpawnNotesInPhrase(ScrollingPhraseNoteTracker tracker, int harmonyIndex)
        {
            var pool = _notePools[harmonyIndex];

            while (tracker.CurrentNoteInBounds && tracker.CurrentNote.Time <= GameManager.SongTime + SpawnTimeOffset)
            {
                var note = tracker.CurrentNote;

                if (note.IsNonPitched)
                {
                    // Skip this frame if the pool is full
                    if (!_talkiePool.CanSpawnAmount(1))
                    {
                        return;
                    }

                    // Spawn the vocal note
                    var noteObj = _talkiePool.TakeWithoutEnabling();
                    ((VocalTalkieElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }
                else if (!note.IsPercussion)
                {
                    // Skip this frame if the pool is full
                    if (!pool.CanSpawnAmount(1))
                    {
                        return;
                    }

                    // Spawn the vocal note
                    var noteObj = pool.TakeWithoutEnabling();
                    ((VocalNoteElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }

                tracker.NextNote();
            }
        }

        private void SpawnScrollingLyrics(ScrollingPhraseNoteTracker tracker, int harmonyIndex)
        {
            int spawnedThisFrame = 0;
            while (tracker.CurrentLyricInBounds &&
                tracker.CurrentLyric.Time <= GameManager.SongTime + SpawnTimeOffset &&
                spawnedThisFrame < SCROLLING_LYRIC_SPAWN_BUDGET)
            {
                var spawnResult = _lyricContainer.TrySpawnScrollingLyric(
                    _preparedScrollingLyrics[tracker.CurrentLyric],
                    AllowStarPower && tracker.CurrentPhrase.IsStarPower,
                    _totalHarms,
                    harmonyIndex);

                if (spawnResult == VocalLyricContainer.LyricSpawnResult.PoolUnavailable)
                {
                    return;
                }

                if (spawnResult == VocalLyricContainer.LyricSpawnResult.Spawned)
                {
                    spawnedThisFrame++;
                }

                tracker.NextLyric();
            }
        }

        private void SpawnStaticLyrics(StaticPhraseTracker tracker, int harmonyIndex)
        {
            if (_noMoreStaticPhrases[harmonyIndex])
            {
                return;
            }

            var change = tracker.UpdateCurrentPhrase(GameManager.SongTime);
            var queue = _staticPhraseQueues[harmonyIndex];

            EnqueueStaticPhrases(harmonyIndex, STATIC_PHRASE_ENQUEUE_BUDGET);

            switch (change)
            {
                case StaticLyricShiftType.None:
                    break;
                case StaticLyricShiftType.PhraseToGap:
                {
                    if (queue.Count == 0)
                    {
                        return;
                    }

                    var leftmostPhraseElement = queue.Dequeue();
                    var leftShift = leftmostPhraseElement.Width;

                    foreach (var remainingPhrase in queue)
                    {
                        remainingPhrase.transform.DOLocalMoveX(remainingPhrase.transform.localPosition.x - leftShift, STATIC_LYRIC_SHIFT_DURATION);

                    }
                    _rightEdges[harmonyIndex] -= leftShift;
                    leftmostPhraseElement.Dismiss();
                    break;
                }
                case StaticLyricShiftType.PhraseToPhrase:
                {
                    if (queue.Count < 2)
                    {
                        EnqueueStaticPhrases(harmonyIndex, 2 - queue.Count);
                    }

                    if (queue.Count < 2)
                    {
                        return;
                    }

                    var leftmostPhraseElement = queue.Dequeue();
                    var leftShift = leftmostPhraseElement.Width + VocalLyricContainer.STATIC_PHRASE_SPACING;
                    queue.Peek().Activate();

                    foreach (var remainingPhrase in queue)
                    {
                        remainingPhrase.transform.DOLocalMoveX(remainingPhrase.transform.localPosition.x - leftShift,
                            Mathf.Min(STATIC_LYRIC_SHIFT_DURATION, (float)leftmostPhraseElement.Duration));

                    }
                    _rightEdges[harmonyIndex] -= leftShift;
                    leftmostPhraseElement.Dismiss();
                    break;
                }
                case StaticLyricShiftType.GapToPhrase:
                {
                    if (queue.Count < 1)
                    {
                        EnqueueStaticPhrases(harmonyIndex, 1);
                    }

                    if (queue.Count < 1)
                    {
                        return;
                    }

                    var leftmostPhraseElement = queue.Peek();

                    _rightEdges[harmonyIndex] -= VocalLyricContainer.STATIC_PHRASE_SPACING;
                    foreach (var remainingPhrase in queue)
                    {
                        remainingPhrase.transform.DOLocalMoveX(
                            remainingPhrase.transform.localPosition.x - VocalLyricContainer.STATIC_PHRASE_SPACING,
                            Mathf.Min(STATIC_LYRIC_SHIFT_DURATION, (float)leftmostPhraseElement.Duration));

                    }
                    leftmostPhraseElement.Activate();
                    break;
                }
                case StaticLyricShiftType.FinalPhraseComplete:
                {
                    _noMoreStaticPhrases[harmonyIndex] = true;
                    if (queue.Count == 0)
                    {
                        break;
                    }

                    var finalPhraseElement = queue.Dequeue();
                    finalPhraseElement.Dismiss();
                    break;
                }
                case StaticLyricShiftType.NoPhrases:
                    _noMoreStaticPhrases[harmonyIndex] = true;
                    break;
            }
        }

        private void EnqueueStaticPhrases(int harmonyIndex, int budget)
        {
            var queue = _staticPhraseQueues[harmonyIndex];
            var preparedPhrases = _preparedStaticPhrases[harmonyIndex];
            int enqueued = 0;

            // Enqueue more phrases, if we have room
            for (var phraseIdx = _highestEnqueuedPhrasePairIndices[harmonyIndex] + 1;
                phraseIdx < preparedPhrases.Count && enqueued < budget;
                phraseIdx++)
            {
                if (queue.Count >= MAXIMUM_STATIC_PHRASE_QUEUE_SIZE)
                {
                    break;
                }

                var phrase = preparedPhrases[phraseIdx];

                if (phrase.PhrasePair.IsPercussion)
                {
                    continue;
                }

                var newPhraseElement = _lyricContainer.TrySpawnStaticLyricPhrase(
                    phrase, _totalHarms, harmonyIndex, _rightEdges[harmonyIndex]);

                if (newPhraseElement != null)
                {
                    _rightEdges[harmonyIndex] += newPhraseElement.Width + VocalLyricContainer.STATIC_PHRASE_SPACING;
                    _highestEnqueuedPhrasePairIndices[harmonyIndex] = phraseIdx;
                    queue.Enqueue(newPhraseElement);
                    enqueued++;
                }
                else
                {
                    break;
                }
            }
        }

        private void SpawnPhraseLines(int harmonyIndex)
        {
            var phrases = _vocalsTrack.Parts[harmonyIndex].NotePhrases;
            int index = _phraseMarkerIndices[harmonyIndex];

            while (index < phrases.Count && phrases[index].TimeEnd <= GameManager.SongTime + SpawnTimeOffset)
            {
                // Spawn the phrase end line
                var poolable = _phraseLinePool.TakeWithoutEnabling();
                ((PhraseLineElement) poolable).PhraseRef = phrases[index];
                poolable.EnableFromPool();

                index++;
            }

            // Update the index value
            _phraseMarkerIndices[harmonyIndex] = index;
        }

        private void PrepareLyricSpawns()
        {
            _preparedScrollingLyrics = new();
            for (int partIndex = 0; partIndex < _vocalsTrack.Parts.Count; partIndex++)
            {
                var part = _vocalsTrack.Parts[partIndex];

                foreach (var phrase in part.NotePhrases)
                {
                    foreach (var lyric in phrase.Lyrics)
                    {
                        var probableNote = phrase.PhraseParentNote.ChildNotes
                            .FirstOrDefault(note => note.Tick == lyric.Tick);
                        _preparedScrollingLyrics[lyric] =
                            _lyricContainer.PrepareScrollingLyric(lyric, probableNote, _totalHarms, partIndex);
                    }
                }
            }

            _preparedStaticPhrases = new List<VocalStaticLyricPhraseElement.PreparedPhrase>[_staticPhraseTrackers.Length];
            for (int harmonyIndex = 0; harmonyIndex < _staticPhraseTrackers.Length; harmonyIndex++)
            {
                var tracker = _staticPhraseTrackers[harmonyIndex];
                if (tracker == null)
                {
                    _preparedStaticPhrases[harmonyIndex] = new();
                    continue;
                }

                var preparedPhrases = new List<VocalStaticLyricPhraseElement.PreparedPhrase>(tracker.PhrasePairs.Count);
                foreach (var phrasePair in tracker.PhrasePairs)
                {
                    preparedPhrases.Add(_lyricContainer.PrepareStaticLyricPhrase(
                        phrasePair,
                        _vocalsTrack.Parts[harmonyIndex].NotePhrases,
                        _totalHarms,
                        harmonyIndex));
                }

                _preparedStaticPhrases[harmonyIndex] = preparedPhrases;
            }
        }

        private void PrewarmVocalPools()
        {
            var visibleWindow = SpawnTimeOffset + (10f / TrackSpeed);
            var scrollingTimesByLane = new List<double>[] { new(), new(), new() };
            var phraseLineTimes = new List<double>();
            var talkieTimes = new List<double>();

            for (int harmonyIndex = 0; harmonyIndex < _vocalsTrack.Parts.Count; harmonyIndex++)
            {
                var part = _vocalsTrack.Parts[harmonyIndex];
                int laneIndex = VocalLyricContainer.GetLaneIndex(_totalHarms, harmonyIndex);
                var noteTimes = new List<double>();
                double longestNoteLength = 0;

                foreach (var phrase in part.NotePhrases)
                {
                    phraseLineTimes.Add(phrase.TimeEnd);

                    foreach (var lyric in phrase.Lyrics)
                    {
                        if (_preparedScrollingLyrics.TryGetValue(lyric, out var preparedLyric) && !preparedLyric.IsHidden)
                        {
                            scrollingTimesByLane[laneIndex].Add(lyric.Time);
                        }
                    }

                    foreach (var note in phrase.PhraseParentNote.ChildNotes)
                    {
                        longestNoteLength = Math.Max(longestNoteLength, note.TotalTimeLength);
                        if (note.IsNonPitched)
                        {
                            talkieTimes.Add(note.Time);
                        }
                        else if (!note.IsPercussion)
                        {
                            noteTimes.Add(note.Time);
                        }
                    }
                }

                _notePools[harmonyIndex].PrewarmTo(Math.Max(5,
                    GetMaxEventsInWindow(noteTimes, visibleWindow + longestNoteLength)));
            }

            for (int laneIndex = 0; laneIndex < scrollingTimesByLane.Length; laneIndex++)
            {
                _lyricContainer.PrewarmScrollingPool(laneIndex, Math.Max(5,
                    GetMaxEventsInWindow(scrollingTimesByLane[laneIndex], visibleWindow)));
            }

            for (int harmonyIndex = 0; harmonyIndex < LyricLaneCount; harmonyIndex++)
            {
                int laneIndex = VocalLyricContainer.GetLaneIndex(_totalHarms, harmonyIndex);
                _lyricContainer.PrewarmStaticPool(laneIndex, MAXIMUM_STATIC_PHRASE_QUEUE_SIZE);
            }

            _phraseLinePool.PrewarmTo(Math.Max(3, GetMaxEventsInWindow(phraseLineTimes, visibleWindow)));
            _talkiePool.PrewarmTo(Math.Max(5, GetMaxEventsInWindow(talkieTimes, visibleWindow)));
        }

        private static int GetMaxEventsInWindow(List<double> times, double window)
        {
            if (times.Count == 0)
            {
                return 0;
            }

            times.Sort();

            int max = 0;
            int left = 0;
            for (int right = 0; right < times.Count; right++)
            {
                while (times[right] - times[left] > window)
                {
                    left++;
                }

                max = Math.Max(max, right - left + 1);
            }

            return max;
        }
    }
}
