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
            StartTransitionEnable = startTransitionEnable;
            EndTransitionEnable = endTransitionEnable;
        }

        public readonly double Time;
        public readonly double TimeEnd;
        public readonly TrackEffectType EffectType;
        public bool StartTransitionEnable {get; private set;}
        public bool EndTransitionEnable {get; private set;}

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
        public bool Contains(TrackEffect other) => Time < other.Time && other.TimeEnd < TimeEnd;

        // Takes a list of track effects, sorts, slices, and dices, chops,
        // and blends in order to create just the right combination
        // of non-overlapping effects to delight and surprise users

        // TODO: Handle edge overlap, too. The most common case of a drum
        //  fill end and unison start having equal times is handled, but
        //  we would ideally also look for cases where the times are not
        //  equal, but fall within the space of the transition and adjust
        //  the start and end times of the visuals to actually abut and
        //  disable the end/start transition effect for the corresponding
        //  effects.
        public static List<TrackEffect> SliceEffects(params List<TrackEffect>[] trackEffects)
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

                if (!effect.Overlaps(nextEffect))
                {
                    // There is no overlap, so no need to slice
                    // We do need to check for adjacency, though!
                    if (effect.TimeEnd == nextEffect.Time)
                    {
                        // There is adjacency, so disable the transitions
                        // TODO: Make this handle the case where only the transitions overlap
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
                    slicedEffects.Add(new TrackEffect(effect.Time, nextEffect.Time,
                        effect.EffectType, effect.StartTransitionEnable, false));
                    var newEffect = new TrackEffect(nextEffect.Time, nextEffect.TimeEnd,
                        GetEffectCombination(effect, nextEffect), false, nextEffect.EndTransitionEnable);
                    if (!(q.TryPeek(out var nextNextEffect) && newEffect.Contains(nextNextEffect)))
                    {
                        // Remainder of outer effect contains no more inner effects
                        slicedEffects.Add(newEffect);
                        slicedEffects.Add(new TrackEffect(newEffect.TimeEnd, effect.TimeEnd, effect.EffectType, false, effect.EndTransitionEnable));
                        q.Dequeue();
                        continue;
                    }

                    // There are more inner effects, so process them until we run out

                    // A double ended queue would avoid the nested loop
                    // we have to take because of the possibility of
                    // multiple effects within the outer effect
                    // TODO: Maybe do this with a list instead even though it will have to have a bunch of bounds checks?
                    while (q.TryPeek(out nextNextEffect) && effect.Contains(nextNextEffect))
                    {
                        if (newEffect.TimeEnd < nextNextEffect.Time)
                        {
                            // There is a gap, so fill it with outer effect
                            slicedEffects.Add(new TrackEffect(newEffect.TimeEnd, nextNextEffect.Time,
                                effect.EffectType, false, false));
                        }

                        // now the inner effect
                        newEffect = new TrackEffect(nextNextEffect.Time, nextNextEffect.TimeEnd,
                            GetEffectCombination(effect, nextNextEffect), false, false);
                        slicedEffects.Add(newEffect);
                        q.Dequeue();
                    }

                    continue;
                }
                // There is overlap, but next is not contained in current
                // Create three sections, current alone, combination, next alone
                // TODO: Deal with the case where next next is contained within or overlaps next
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

        static TrackEffectType GetEffectCombination(TrackEffect outer, TrackEffect inner)
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
                    TrackEffectType.Unison => TrackEffectType.DrumFillAndUnison,
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

                    effects.Add(
                        new TrackEffect(phrases[i].Time, phrases[i].TimeEnd, (TrackEffectType) kind, true, true));
                }
            }
            return effects;
        }
    }
}