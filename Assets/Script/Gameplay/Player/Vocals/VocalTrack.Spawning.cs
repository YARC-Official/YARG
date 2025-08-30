using DG.Tweening;
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
        private const float MAXIMUM_STATIC_PHRASE_QUEUE_SIZE = 10;
        private const float STATIC_LYRIC_SHIFT_DURATION = .1f;

        private ScrollingPhraseNoteTracker[] _scrollingNoteTrackers;
        private ScrollingPhraseNoteTracker[] _scrollingLyricTrackers;
        private StaticPhraseTracker[] _staticPhraseTrackers;
        private Queue<VocalStaticLyricPhraseElement>[] _staticPhraseQueues;


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
            }

            for (int i = 0; i < LyricLaneCount; i++)
            {
                SpawnLyrics(_scrollingLyricTrackers[i], _staticPhraseTrackers[i], i);
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

        private void SpawnLyrics(ScrollingPhraseNoteTracker scrollingTracker, StaticPhraseTracker staticTracker, int harmonyIndex)
        {
            if (SettingsManager.Settings.StaticVocalsMode.Value)
            {
                SpawnStaticLyrics(staticTracker, harmonyIndex);
            } else
            {
                SpawnScrollingLyrics(scrollingTracker, harmonyIndex);
            }
        }

        private void SpawnScrollingLyrics(ScrollingPhraseNoteTracker tracker, int harmonyIndex)
        {
            while (tracker.CurrentLyricInBounds && tracker.CurrentLyric.Time <= GameManager.SongTime + SpawnTimeOffset)
            {
                if (!_lyricContainer.TrySpawnScrollingLyric(
                    tracker.CurrentLyric,
                    tracker.GetProbableNoteAtLyric(),
                    AllowStarPower && tracker.CurrentPhrase.IsStarPower,
                    harmonyIndex))
                {
                    tracker.NextLyric();
                    return;
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

            switch (change)
            {
                case StaticLyricShiftType.None:
                    break;
                case StaticLyricShiftType.PhraseToGap:
                {
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
                    var finalPhraseElement = queue.Dequeue();
                    finalPhraseElement.Dismiss();
                    break;
                }
                case StaticLyricShiftType.NoPhrases:
                    _noMoreStaticPhrases[harmonyIndex] = true;
                    break;
            }

            // Enqueue more phrases, if we have room
            for (var phraseIdx = _highestEnqueuedPhrasePairIndices[harmonyIndex] + 1; phraseIdx < _staticPhraseTrackers[harmonyIndex].PhrasePairs.Count; phraseIdx++)
            {
                if (queue.Count > MAXIMUM_STATIC_PHRASE_QUEUE_SIZE)
                {
                    break;
                }

                var phrase = _staticPhraseTrackers[harmonyIndex].PhrasePairs[phraseIdx];

                if (phrase.IsPercussion)
                {
                    continue;
                }

                var newPhraseElement = _lyricContainer.TrySpawnStaticLyricPhrase(phrase, _vocalsTrack.Parts[harmonyIndex].NotePhrases, harmonyIndex, _rightEdges[harmonyIndex]);

                if (newPhraseElement != null)
                {
                    _rightEdges[harmonyIndex] += newPhraseElement.Width + VocalLyricContainer.STATIC_PHRASE_SPACING;
                    _highestEnqueuedPhrasePairIndices[harmonyIndex] = phraseIdx;
                    queue.Enqueue(newPhraseElement);
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
    }
}