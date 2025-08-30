using Cysharp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Settings;
using static YARG.Gameplay.Player.VocalTrack;

namespace YARG.Gameplay.Visuals
{
    public class VocalStaticLyricPhraseElement : BaseElement // not a VocalElement because it doesn't scroll along the highway
    {
        private const string PAST_LYRIC_COLOR_TAG = "<color=#595959>";
        private const string PRESENT_LYRIC_COLOR_TAG = "<color=#5CB9FF>";
        private const string FUTURE_LYRIC_COLOR_TAG = "<color=#FFFFFF>";
        private const string FUTURE_PHRASE_COLOR_TAG = "<color=#595959>";
        private const string CLOSE_COLOR_TAG = "</color>";

        private VocalPhrasePair _phrasePairRef;
        private VocalStaticLyricPhraseElement? _previousPhraseElement;
        private bool _isStarpower;
        private int _harmonyIndex;
        private bool _allowHiding;
        private float _x;
        private bool _isFuture = true;

        private Utf16ValueStringBuilder _builder;

        private List<StaticLyricSyllable> _syllables = new();

        public override double ElementTime => _phrasePairRef.Time;

        [SerializeField]
        private TextMeshPro _phraseText;

        public float Width => _phraseText.GetPreferredValues().x;

        public double Duration => _phrasePairRef.Duration;

        public void Initialize(VocalPhrasePair phrasePair, bool isStarpower, int harmonyIndex,
            bool allowHiding, float x)
        {
            _phrasePairRef = phrasePair;
            _isStarpower = isStarpower;
            _harmonyIndex = harmonyIndex;
            _allowHiding = allowHiding;
            _x = x;

            _builder = ZString.CreateStringBuilder(false);
        }

        protected override void InitializeElement()
        {
            var mergedLyricIdx = 0;

            var mainPhrase = _phrasePairRef.MainPhrase;
            var mergedPhrase = _phrasePairRef.MergedPhrase;

            // Handle HARM3-only phrases
            if (mainPhrase is null)
            {
                while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                {
                    var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;

                    var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                    var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);
                    MakeStaticLyricSyllable(mergedLyric.Text, mergedLyric.Time, probableMergedLyricEnd, mergedLyric.Flags, isLastLyricOfMergedPhrase);
                }
            }

            else {
                for (var mainLyricIdx = 0; mainLyricIdx < mainPhrase.Lyrics.Count; mainLyricIdx++)
                {
                    var mainLyric = mainPhrase.Lyrics[mainLyricIdx];
                    var probableMainLyricEnd = GetProbableNoteEndOfLyric(mainPhrase, mainLyric);
                    var isLastLyricOfMainPhrase = mainLyricIdx == mainPhrase.Lyrics.Count - 1;

                    if (mergedPhrase is not null)
                    {
                        // Handle any merged lyrics that happened before the current lyric
                        while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                        {
                            if (mergedPhrase.Lyrics[mergedLyricIdx].Time >= mainPhrase.Lyrics[mainLyricIdx].Time)
                            {
                                break;
                            }

                            var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                            var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);

                            // isLastLyricOfPhrase is definitely false, because we still have at least one main phrase lyric to add
                            MakeStaticLyricSyllable(mergedLyric.Text, mergedLyric.Time, probableMergedLyricEnd, mergedLyric.Flags, false);
                        }
                    }

                    bool mainLyricIsLastLyricOfEntirePhrase; // Including both the main and merged lyrics
                    if (isLastLyricOfMainPhrase)
                    {
                        // This is the last lyric of the main phrase, but what about the merged phrase?
                        mainLyricIsLastLyricOfEntirePhrase = mergedPhrase is not null && mergedLyricIdx < mergedPhrase.Lyrics.Count - 1;
                    } else
                    {
                        // This isn't even the last lyric of the main phrase, so it's definitely not the last one overall
                        mainLyricIsLastLyricOfEntirePhrase = false;
                    }

                    MakeStaticLyricSyllable(mainLyric.Text, mainLyric.Time, probableMainLyricEnd, mainLyric.Flags, mainLyricIsLastLyricOfEntirePhrase);

                    // If there's a simultaneous syllable in the merged part...
                    if (mergedPhrase is not null && mergedLyricIdx < mergedPhrase.Lyrics.Count && mergedPhrase.Lyrics[mergedLyricIdx].Time == mainLyric.Time)
                    {
                        var simultaneousMergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];

                        // ...and its text isn't an exact match to the main syllable...
                        if (simultaneousMergedLyric.Text != mainLyric.Text)
                        {
                            var probableSimultaneousMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, simultaneousMergedLyric);
                            var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;

                            // ...add it after the main syllable
                            MakeStaticLyricSyllable(
                                simultaneousMergedLyric.Text,
                                simultaneousMergedLyric.Time,
                                probableSimultaneousMergedLyricEnd,
                                simultaneousMergedLyric.Flags,
                                mainLyricIsLastLyricOfEntirePhrase && mergedLyricIdx == mergedPhrase.Lyrics.Count - 1
                            );
                        }
                    }
                }

                // Handle any remaining merged lyrics after the last main phrase lyric
                if (mergedPhrase is not null)
                {
                    while (mergedLyricIdx < mergedPhrase.Lyrics.Count)
                    {
                        var mergedLyric = mergedPhrase.Lyrics[mergedLyricIdx++];
                        var probableMergedLyricEnd = GetProbableNoteEndOfLyric(mergedPhrase, mergedLyric);
                        var isLastLyricOfMergedPhrase = mergedLyricIdx == mergedPhrase.Lyrics.Count - 1;
                        MakeStaticLyricSyllable(mergedLyric.Text, mergedLyric.Time, probableMergedLyricEnd, mergedLyric.Flags, mergedLyricIdx == mergedPhrase.Lyrics.Count - 1);
                    }
                }
            }

            transform.localPosition = transform.localPosition.WithX(_x);

            _builder.Append(FUTURE_PHRASE_COLOR_TAG);

            foreach (var syllable in _syllables)
            {
                _builder.Append(syllable.Text);
            }

            _builder.Append(CLOSE_COLOR_TAG);

            _phraseText.text = _builder.ToString();
        }

        public void Activate()
        {
            _isFuture = false;
        }

        public void Dismiss()
        {
            _isFuture = true;
            DisableIntoPool();
            ParentPool.Return(this);
        }

        protected override void UpdateElement()
        {
            if (_isFuture)
            {
                return;
            }

            _builder.Clear();

            foreach (var syllable in _syllables)
            {
                if (GameManager.SongTime < syllable.Time)
                {
                    BuilderAppendWithColorTag(syllable.Text, FUTURE_LYRIC_COLOR_TAG);
                }
                else if (syllable.Time <= GameManager.SongTime && GameManager.SongTime < syllable.TimeEnd)
                {
                    BuilderAppendWithColorTag(syllable.Text, PRESENT_LYRIC_COLOR_TAG);
                }
                else {
                    BuilderAppendWithColorTag(syllable.Text, PAST_LYRIC_COLOR_TAG);
                }
            }

            _phraseText.text = _builder.ToString();
        }

        protected override bool UpdateElementPosition()
        {
            return true;
        }

        protected override void HideElement()
        {
        }

        private void BuilderAppendWithColorTag(string text, string colorTag)
        {
            _builder.Append(colorTag);
            _builder.Append(text);
            _builder.Append(CLOSE_COLOR_TAG);
        }

        private static double GetProbableNoteEndOfLyric(VocalsPhrase phrase, LyricEvent lyric)
        {
            return phrase.PhraseParentNote.ChildNotes
                .FirstOrDefault(note => note.Tick == lyric.Tick).TotalTimeEnd;
        }

        private void MakeStaticLyricSyllable(string text, double time, double timeEnd, LyricSymbolFlags flags, bool isLastLyricOfPhrase)
        {
            if (!SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value && ((flags & LyricSymbolFlags.HarmonyHidden) != 0))
            {
                return;
            }

            _syllables.Add(new(text, time, timeEnd, flags, isLastLyricOfPhrase));
        }

        private struct StaticLyricSyllable
        {
            public string Text;
            public double Time;
            public double TimeEnd;

            public StaticLyricSyllable(string text, double time, double timeEnd, LyricSymbolFlags flags, bool isLastLyricOfPhrase)
            {
                var builder = ZString.CreateStringBuilder(false);

                Time = time;
                TimeEnd = timeEnd;

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("<i>");
                }

                if ((flags & LyricSymbolFlags.JoinWithNext) != 0)
                {
                    builder.Append(text[0..^1]);
                    if (text.EndsWith("="))
                    {
                        builder.Append("-");
                    }
                } else
                {
                    builder.Append(text);
                }

                if ((flags & LyricSymbolFlags.NonPitched) != 0)
                {
                    builder.Append("</i>");
                }

                if (!isLastLyricOfPhrase && ((flags & LyricSymbolFlags.JoinWithNext) == 0))
                {
                    builder.Append(" ");
                }

                Text = builder.ToString();
            }
        }
    }
}
