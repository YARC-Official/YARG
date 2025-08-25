using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public enum TrackEffectType
    {
        Solo,
        Unison,
        SoloAndUnison,
        DrumFill,
        SoloAndDrumFill,
        DrumFillAndUnison,
    }

    // Small warning, TrackEffects are equal if their Time and TimeEnd values
    // match, regardless of whether or not they have different EffectType values
    public class TrackEffect : IComparable<TrackEffect>, IEquatable<TrackEffect>
    {
        public TrackEffect(double time, double timeEnd, TrackEffectType effectType,
            bool startTransitionEnable = true, bool endTransitionEnable = true)
        {
            Time = time;
            TimeEnd = timeEnd;
            EffectType = effectType;
            OriginalEffectType = effectType;
            StartTransitionEnable = startTransitionEnable;
            OriginalStartTransitionEnable = startTransitionEnable;
            EndTransitionEnable = endTransitionEnable;
            OriginalEndTransitionEnable = endTransitionEnable;
        }

        // This is the scale of the transition object (currently 0.005) * 100
        // If the scale is changed or the object is changed from a plane this
        // will have to change
        private const float TRANSITION_SCALE = 0.5f;

        public double Time { get; private set; }
        public double TimeEnd { get; private set; }
        public TrackEffectType EffectType { get; set; }
        public readonly TrackEffectType OriginalEffectType;
        public bool StartTransitionEnable { get; set; }
        public bool EndTransitionEnable { get; set; }
        public bool OriginalEndTransitionEnable { get; private set; }

        public bool OriginalStartTransitionEnable { get; private set; }
        public float Visibility { get; set; } = 1.0f;

        public bool Equals(TrackEffect other) => other is not null && Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);
        public override bool Equals(object obj) => obj is TrackEffect && Equals((TrackEffect)obj);
        public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);
        public int CompareTo(TrackEffect other) => Time.CompareTo(other.Time);

        public static bool operator ==(TrackEffect left, TrackEffect right) => left is not null && left.Equals(right);
        public static bool operator !=(TrackEffect left, TrackEffect right) => !(left == right);
        public static bool operator <(TrackEffect left, TrackEffect right) => left.CompareTo(right) < 0;
        public static bool operator >(TrackEffect left, TrackEffect right) => left.CompareTo(right) > 0;
        public static bool operator <=(TrackEffect left, TrackEffect right) => left.CompareTo(right) <= 0;
        public static bool operator >=(TrackEffect left, TrackEffect right) => left.CompareTo(right) >= 0;

        public bool Overlaps(TrackEffect other) => Time < other.TimeEnd && other.Time < TimeEnd;
        public bool Contains(TrackEffect other) => Time <= other.Time && other.TimeEnd <= TimeEnd;

        // Takes a list of track effects, sorts, slices, and dices, chops,
        // and blends in order to create just the right combination
        // of non-overlapping effects to delight and surprise users
        public static List<TrackEffect> SliceEffects(float noteSpeed, params List<TrackEffect>[] trackEffects)
        {
            var fullEffectsList = new List<TrackEffect>();
            foreach (var effectList in trackEffects)
            {
                fullEffectsList.AddRange(effectList);
            }
            fullEffectsList.Sort();

            var slicedEffects = new List<TrackEffect>();
            var effectsToProcess = new List<TrackEffect>(fullEffectsList);

            while (effectsToProcess.Count > 0)
            {
                var currentEffect = effectsToProcess[0];
                effectsToProcess.RemoveAt(0);

                if (effectsToProcess.Count == 0)
                {
                    // We're at the end of the list, so just add the current effect and break
                    slicedEffects.Add(currentEffect);
                    break;
                }

                // This is down here to avoid triggering an out of range exception at the end of the list
                var nextEffect = effectsToProcess[0];

                // Handle adjacency and transition overlap that doesn't involve overlap of the actual effects
                HandleTransitions(currentEffect, nextEffect, noteSpeed);


                if (!currentEffect.Overlaps(nextEffect))
                {
                    // No overlap, so no need for further processing
                    slicedEffects.Add(currentEffect);
                    continue;
                }

                if (currentEffect.Contains(nextEffect))
                {
                    // The current effect contains the next effect, so process it
                    ProcessContainedEffect(currentEffect, nextEffect, slicedEffects, effectsToProcess);
                }
                else
                {
                    // We wouldn't be here if there weren't some kind of overlap and next isn't contained in current,
                    // so it must be a partial overlap
                    ProcessPartialOverlap(currentEffect, nextEffect, slicedEffects);
                    // Next has been processed, so remove it
                    effectsToProcess.RemoveAt(0);

                    // This isn't super obvious, but the remaining part of outer needs to be processed in full
                    // as it does occasionally happen that it will contain or overlap with another effect
                    var remainingEffect = new TrackEffect(nextEffect.TimeEnd, currentEffect.TimeEnd,
                        currentEffect.EffectType, false, nextEffect.EndTransitionEnable);
                    effectsToProcess.Insert(0, remainingEffect);

                }
            }

            return slicedEffects;
        }

        private static void HandleTransitions(TrackEffect current, TrackEffect next, float noteSpeed)
        {
            // If current contains next, we don't actually want to do anything
            if (current.Contains(next))
            {
                return;
            }

            // The number of transitions enabled in this comparison set
            var numEnabled = current.EndTransitionEnable ? 1 + (next.StartTransitionEnable ? 1 : 0) : 0 + (next.StartTransitionEnable ? 1 : 0);
            var minTime = numEnabled > 0 ? TRANSITION_SCALE * numEnabled / noteSpeed : 0f;

            if (current.TimeEnd == next.Time)
            {
                // There is adjacency, so disable the transitions
                current.EndTransitionEnable = false;
                next.StartTransitionEnable = false;
            }
            else if (next.Time - current.TimeEnd < minTime)
            {
                // Too close, so adjust them to be adjacent
                var adjustedTime = ((next.Time - current.TimeEnd) / 2) + current.TimeEnd;
                next.Time = adjustedTime;
                current.TimeEnd = adjustedTime;
                current.EndTransitionEnable = false;
                next.StartTransitionEnable = false;
            }
        }

        private static void ProcessContainedEffect(TrackEffect outer, TrackEffect inner,
            List<TrackEffect> slicedEffects,
            List<TrackEffect> remainingEffects)
        {
            // Add segment of outer effect lasting until the beginning of the overlap
            if (outer.Time < inner.Time)
            {
                slicedEffects.Add(new TrackEffect(outer.Time, inner.Time, outer.EffectType, outer.StartTransitionEnable,
                    false));
            }

            // Add inner effect(s)
            var innerEnd = ProcessInnerEffects(outer, inner, slicedEffects, remainingEffects);

            // Add any remaining outer effect
            if (innerEnd < outer.TimeEnd)
            {
                slicedEffects.Add(new TrackEffect(innerEnd, outer.TimeEnd, outer.EffectType, false,
                    outer.EndTransitionEnable));
            }
        }

        private static double ProcessInnerEffects(TrackEffect outer, TrackEffect inner, List<TrackEffect> slicedEffects,
            List<TrackEffect> remainingEffects)
        {
            var currentEnd = inner.TimeEnd;
            slicedEffects.Add(new TrackEffect(inner.Time, inner.TimeEnd, GetEffectCombination(outer, inner),
                false, false));
            remainingEffects.RemoveAt(0);

            while (remainingEffects.Count > 0 && (outer.Contains(remainingEffects[0]) || outer.Overlaps(remainingEffects[0])))
            {
                if (currentEnd < remainingEffects[0].Time)
                {
                    // Fill the gap between inner effects with outer effect
                    slicedEffects.Add(new TrackEffect(slicedEffects[^1].TimeEnd, remainingEffects[0].Time,
                        outer.EffectType, false, false));;
                }

                // If outer does not contain inner, split outer at the end of the previous contained effect
                // (one will definitely exist because this code path can't be reached without at least one
                // inner being fully contained), add it to the head of the list of effects so SliceEffect
                // will handle it as an overlap on its next iteration, and return
                if (!outer.Contains(remainingEffects[0]))
                {
                    var remainderOfOuter = new TrackEffect(slicedEffects[^1].TimeEnd, outer.TimeEnd,
                        outer.EffectType, false, outer.EndTransitionEnable);
                    remainingEffects.Insert(0, remainderOfOuter);
                    // This is a little white lie, but it keeps ProcessContainedEffect from adding more outer
                    return outer.TimeEnd;
                }

                // Add the combination of inner+outer
                slicedEffects.Add(new TrackEffect(remainingEffects[0].Time, remainingEffects[0].TimeEnd,
                    GetEffectCombination(outer, remainingEffects[0]), false, false));
                currentEnd = remainingEffects[0].TimeEnd;
                remainingEffects.RemoveAt(0);
            }

            return currentEnd;
        }

        private static void ProcessPartialOverlap(TrackEffect current, TrackEffect next,
            List<TrackEffect> slicedEffects)
        {
            // Add non-overlapping part
            slicedEffects.Add(new TrackEffect(current.Time, next.Time, current.EffectType, current.StartTransitionEnable,
                false));
            // Add the overlapping part
            slicedEffects.Add(new TrackEffect(next.Time, next.TimeEnd, GetEffectCombination(current, next),
                false, next.EndTransitionEnable));
        }

        private static TrackEffectType GetEffectCombination(TrackEffect outer, TrackEffect inner)
        {
            TrackEffectType? combo = null;
            if (outer.EffectType == TrackEffectType.Solo)
            {
                combo = inner.EffectType switch
                {
                    TrackEffectType.Unison   => TrackEffectType.SoloAndUnison,
                    TrackEffectType.DrumFill => TrackEffectType.SoloAndDrumFill,
                    // If we don't know what else to do, just use the outer type
                    _ => null
                };
            }
            // I'm not sure if this is even chartable
            if (outer.EffectType == TrackEffectType.DrumFill)
            {
                combo = inner.EffectType switch
                {
                    // By request of the art department, unison phrases in drum fills are
                    // rendered as if there was no unison
                    TrackEffectType.Unison => TrackEffectType.DrumFill,
                    _                      => null
                };
            }

            if (combo != null)
            {
                return (TrackEffectType) combo;
            }
            YargLogger.LogFormatWarning("Someone charted a {0} in a {1} and TrackEffect doesn't know how to combine that business",
                outer.EffectType, inner.EffectType);
            return outer.EffectType;
        }

        // Give me a list of chart phrases, I give you a list of corresponding track effects
        // I only ask that the phrases you give me are all the same kind
        public static List<TrackEffect> PhrasesToEffects(params List<Phrase>[] arrayOfPhraseLists)
        {
            var effects = new List<TrackEffect>();
            foreach (var phrases in arrayOfPhraseLists)
            {
                for (var i = 0; i < phrases.Count; i++)
                {
                    var type = phrases[i].Type;
                    TrackEffectType? kind = type switch
                    {
                        PhraseType.Solo      => TrackEffectType.Solo,
                        PhraseType.DrumFill  => TrackEffectType.DrumFill,
                        PhraseType.StarPower => TrackEffectType.Unison,
                        _                    => null
                    };
                    if (kind == null)
                    {
                        YargLogger.LogFormatWarning(
                            "TrackEffects given phrase type {0}, which has no corresponding effect", phrases[i].Type);
                        continue;
                    }

                    // Unison and DrumFill no longer use transitions
                    // var transitionState = kind == TrackEffectType.Solo;
                    var transitionState = true;

                    effects.Add(
                        new TrackEffect(phrases[i].Time, phrases[i].TimeEnd, (TrackEffectType) kind, transitionState, transitionState));
                }
            }
            return effects;
        }
    }
}