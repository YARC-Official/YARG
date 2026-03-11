using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Venue.Characters
{
    public class DrumCharacterHelper
    {
        private const double REPEAT_THRESHOLD = 0.125;
        private enum Hand
        {
            LEFT,
            RIGHT,
        }

        private sealed class PadState
        {
            public Hand    DefaultHand  { get; }
            public Hand?   LastUsedHand { get; private set; }
            public double? LastHitTime  { get; private set; }
            public PadState(Hand defaultHand)
            {
                DefaultHand = defaultHand;
            }
            public void RecordHit(Hand selectedHand, double noteTime)
            {
                LastUsedHand = selectedHand;
                LastHitTime = noteTime;
            }
        }

        private readonly IReadOnlyDictionary<FourLaneDrumPad, PadState> _handStateByPad
            = new Dictionary<FourLaneDrumPad, PadState>
            {
                { FourLaneDrumPad.YellowCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.BlueCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.GreenCymbal, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.GreenDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.BlueDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.YellowDrum, new PadState(Hand.RIGHT) },
                { FourLaneDrumPad.RedDrum, new PadState(Hand.LEFT) },
            };

        public List<AnimationEvent> GetDrumAnimations(List<DrumNote> drumNotes)
        {
            var drumAnimationEvents = new List<AnimationEvent>();
            foreach (var parent in drumNotes)
            {
                var animations = GetAnimationsForParentNote(parent);
                foreach (var animation in animations)
                {
                    drumAnimationEvents.Add(new AnimationEvent(animation, parent.Time, parent.TimeLength, parent.Tick, parent.TickLength));
                }
            }

            return drumAnimationEvents;
        }

        private List<AnimationEvent.AnimationType> GetAnimationsForParentNote(DrumNote parentNote)
        {
            var animations = new List<AnimationEvent.AnimationType>();
            var padHands = new List<(FourLaneDrumPad Pad, Hand Hand)>();

            foreach (var note in parentNote.AllNotes)
            {
                var pad = (FourLaneDrumPad) note.Pad;
                if (pad == FourLaneDrumPad.Kick)
                {
                    animations.Add(AnimationEvent.AnimationType.Kick);
                    continue;
                }

                var hand = GetHandForPad(pad, note.Time);
                padHands.Add((pad, hand));
            }

            // Resolve conflicting hands when two non-kick pads want the same hand
            if (padHands.Count >= 2 && padHands[0].Hand == padHands[1].Hand)
            {
                var first = padHands[0];
                var second = padHands[1];
                var firstDefault = _handStateByPad[first.Pad].DefaultHand;
                var secondDefault = _handStateByPad[second.Pad].DefaultHand;

                if (firstDefault != second.Hand)
                {
                    padHands[0] = (first.Pad, firstDefault);
                }
                else if (secondDefault != first.Hand)
                {
                    padHands[1] = (second.Pad, secondDefault);
                }
                else
                {
                    padHands[0] = (first.Pad, AlternateHand(first.Hand));
                }
            }

            foreach (var (pad, hand) in padHands)
            {
                animations.Add(GetAnimationType(pad, hand));
            }

            return animations;
        }

        private Hand GetHandForPad(FourLaneDrumPad pad, double noteTime)
        {
            var state = _handStateByPad.GetValueOrDefault(pad);
            if (state is null)
            {
                YargLogger.LogFormatWarning("Unknown drum pad {0}, defaulting to right hand", pad);
                return Hand.RIGHT;
            }

            var previousHitTime = state.LastHitTime.GetValueOrDefault(double.MaxValue);
            var isRepeatedNote = noteTime - previousHitTime <= REPEAT_THRESHOLD;
            var previousHand = state.LastUsedHand.GetValueOrDefault();
            var shouldAlternateHand = state.LastUsedHand.HasValue && isRepeatedNote;
            var hand = shouldAlternateHand ? AlternateHand(previousHand) : state.DefaultHand;
            state.RecordHit(hand, noteTime);
            return hand;
        }

        private static Hand AlternateHand(Hand hand)
        {
            return hand == Hand.LEFT ? Hand.RIGHT : Hand.LEFT;
        }

        private static AnimationEvent.AnimationType GetAnimationType(FourLaneDrumPad pad, Hand hand)
        {
            return pad switch
            {
                FourLaneDrumPad.RedDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.SnareLhHard
                    : AnimationEvent.AnimationType.SnareRhHard,

                FourLaneDrumPad.YellowCymbal => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.HihatLeftHand
                    : AnimationEvent.AnimationType.HihatRightHand,

                FourLaneDrumPad.BlueCymbal => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.RideLh
                    : AnimationEvent.AnimationType.RideRh,

                FourLaneDrumPad.GreenCymbal => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Crash1LhHard
                    : AnimationEvent.AnimationType.Crash1RhHard,

                FourLaneDrumPad.YellowDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom1LeftHand
                    : AnimationEvent.AnimationType.Tom1RightHand,

                FourLaneDrumPad.BlueDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom2LeftHand
                    : AnimationEvent.AnimationType.Tom2RightHand,

                FourLaneDrumPad.GreenDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.FloorTomLeftHand
                    : AnimationEvent.AnimationType.FloorTomRightHand,

                _ => throw new ArgumentOutOfRangeException(nameof(pad), pad, "Unsupported drum pad for animation mapping."),
            };
        }
    }
}
