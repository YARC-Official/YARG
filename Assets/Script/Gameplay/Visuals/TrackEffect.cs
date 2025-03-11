using System;
using System.Collections.Generic;
using YARG.Core;
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

        public bool Equals(TrackEffect other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);
        public override bool Equals(object obj) => obj is TrackEffect && Equals((TrackEffect)obj);
        public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);
        public int CompareTo(TrackEffect other) => Time.CompareTo(other.Time);

        public static bool operator ==(TrackEffect left, TrackEffect right) => left.Equals(right);
        public static bool operator !=(TrackEffect left, TrackEffect right) => !(left == right);
        public static bool operator <(TrackEffect left, TrackEffect right) => left.CompareTo(right) < 0;
        public static bool operator >(TrackEffect left, TrackEffect right) => left.CompareTo(right) > 0;
        public static bool operator <=(TrackEffect left, TrackEffect right) => left.CompareTo(right) <= 0;
        public static bool operator >=(TrackEffect left, TrackEffect right) => left.CompareTo(right) >= 0;

        public bool Overlaps(TrackEffect other) => !(Time < other.TimeEnd && other.Time >= TimeEnd);
        public bool Contains(TrackEffect other) => Time <= other.Time && other.TimeEnd <= TimeEnd;

        // Takes a list of track effects, sorts, slices, and dices, chops,
        // and blends in order to create just the right combination
        // of non-overlapping effects to delight and surprise users
        public static List<TrackEffect> SliceEffects(float noteSpeed, params List<TrackEffect>[] trackEffects)
        {
            // Combine all the lists we were given, then sort
            var fullEffectsList = new List<TrackEffect>();
            foreach (var effectList in trackEffects)
            {
                fullEffectsList.AddRange(effectList);
            }
            fullEffectsList.Sort();
            var slicedEffects = new List<TrackEffect>();
            var q = new Queue<TrackEffect>(fullEffectsList);

            while (q.TryDequeue(out var effect))
            {
                if (!q.TryPeek(out var nextEffect))
                {
                    // There is no next effect, so overlap is impossible
                    slicedEffects.Add(effect);
                    continue;
                }

                // The number of transitions enabled in this comparison set
                var numEnabled = effect.EndTransitionEnable ? 1 + (nextEffect.StartTransitionEnable ? 1 : 0) : 0 + (nextEffect.StartTransitionEnable ? 1 : 0);

                // The minimum time allowed between effects, taking
                // into account their transition section, if one or more
                // is enabled. This is to avoid transition section overlap
                var minTime = numEnabled > 0 ? TRANSITION_SCALE * numEnabled / noteSpeed : 0f;

                if (!effect.Overlaps(nextEffect))
                {
                    // There is no overlap, so no need to slice
                    // We do need to check for adjacency, though!
                    if (effect.TimeEnd == nextEffect.Time)
                    {
                        // There is adjacency, so disable the transitions
                        effect.EndTransitionEnable = false;
                        nextEffect.StartTransitionEnable = false;
                    }

                    if (nextEffect.Time - effect.TimeEnd < minTime)
                    {
                        // Too close, so adjust them to be adjacent
                        var adjustedTime = ((nextEffect.Time - effect.TimeEnd) / 2) + effect.TimeEnd;
                        nextEffect.Time = adjustedTime;
                        effect.TimeEnd = adjustedTime;
                        effect.EndTransitionEnable = false;
                        nextEffect.StartTransitionEnable = false;
                    }
                    slicedEffects.Add(effect);
                    continue;
                }

                // We reached this point, so there is overlap
                if (effect.Contains(nextEffect))
                {
                    // Add segment of outer effect lasting until beginning of inner effect
                    // Disabled end transition is required
                    // Zero length effect doesn't hurt anything, so we're not wasting the branch
                    slicedEffects.Add(new TrackEffect(effect.Time, nextEffect.Time,
                        effect.EffectType, effect.StartTransitionEnable, false));
                    var newEffect = new TrackEffect(nextEffect.Time, nextEffect.TimeEnd,
                        GetEffectCombination(effect, nextEffect), false, false);
                    if (!(q.TryPeek(out var nextNextEffect) && effect.Contains(nextNextEffect)))
                    {
                        // Remainder of outer effect contains no more inner effects
                        slicedEffects.Add(newEffect);
                        slicedEffects.Add(new TrackEffect(newEffect.TimeEnd, effect.TimeEnd, effect.EffectType, false, effect.EndTransitionEnable));
                        continue;
                    }

                    // Now add inner effect
                    slicedEffects.Add(newEffect);
                    q.Dequeue();

                    // There are more inner effects, so process them until we run out
                    while (q.TryPeek(out nextNextEffect) && effect.Contains(nextNextEffect))
                    {
                        if (newEffect.TimeEnd < nextNextEffect.Time)
                        {
                            // There is a gap, so fill it with outer effect
                            slicedEffects.Add(new TrackEffect(newEffect.TimeEnd, nextNextEffect.Time,
                                effect.EffectType, false, false));
                        }

                        // now the next inner effect
                        newEffect = new TrackEffect(nextNextEffect.Time, nextNextEffect.TimeEnd,
                            GetEffectCombination(effect, nextNextEffect), false, false);
                        slicedEffects.Add(newEffect);
                        q.Dequeue();
                    }
                    // We have exhausted all the inner effects, so add the remainder of the outer effect
                    slicedEffects.Add(new TrackEffect(newEffect.TimeEnd, effect.TimeEnd, effect.EffectType, false, effect.EndTransitionEnable));
                    continue;
                }
                // There is overlap, but next is not contained in current
                // Create three sections, current alone, combination, next alone
                slicedEffects.Add(new TrackEffect(effect.Time, nextEffect.Time, effect.EffectType,
                    effect.StartTransitionEnable, false));
                slicedEffects.Add(new TrackEffect(nextEffect.Time, effect.TimeEnd,
                    GetEffectCombination(effect, nextEffect), false, false));
                slicedEffects.Add(new TrackEffect(effect.TimeEnd, nextEffect.TimeEnd, nextEffect.EffectType, false,
                    nextEffect.EndTransitionEnable));
                q.Dequeue();
            }

            return slicedEffects;
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
                    var transitionState = kind == TrackEffectType.Solo;

                    effects.Add(
                        new TrackEffect(phrases[i].Time, phrases[i].TimeEnd, (TrackEffectType) kind, transitionState, transitionState));
                }
            }
            return effects;
        }
    }
}