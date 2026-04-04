using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Venue.Characters
{
    public class DrumCharacterHelper
    {
        private const double REPEAT_THRESHOLD = 0.125;
        private readonly Random _random = new();

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
                    drumAnimationEvents.Add(
                        new AnimationEvent(animation, parent.Time, parent.TimeLength, parent.Tick, parent.TickLength)
                    );
                }
            }

            return drumAnimationEvents;
        }

        private List<AnimationEvent.AnimationType> GetAnimationsForParentNote(DrumNote parentNote)
        {
            var animations = new List<AnimationEvent.AnimationType>();
            var padHands = new List<(FourLaneDrumPad Pad, Hand Hand)>();

            //Map each pad to a hand
            foreach (var note in parentNote.AllNotes)
            {
                var pad = (FourLaneDrumPad) note.Pad;
                if (pad == FourLaneDrumPad.Kick)
                {
                    continue;
                }
                var hand = GetHandForPad(pad, note.Time);
                padHands.Add((pad, hand));
            }

            // Resolve conflicting hands by switching one hand, preferring to keep blue unchanged
            if (padHands.Count >= 2 && padHands[0].Hand == padHands[1].Hand)
            {
                var indexToSwitch = IsBlueCymbal(padHands[0].Pad) ? 1 : 0;
                var toSwitch = padHands[indexToSwitch];
                padHands[indexToSwitch] = (toSwitch.Pad, SwitchHand(toSwitch.Hand));
            }

            // Special case: consecutive solo snare notes at non-fast tempo use right hand
            var isSoloSnare = padHands.Count == 1 && padHands[0].Pad == FourLaneDrumPad.RedDrum;
            var previousWasSnare = PreviousWasSnareHit(parentNote.PreviousNote);
            var isNotFastRepeat = parentNote.PreviousNote is null
                || parentNote.Time - parentNote.PreviousNote.Time > REPEAT_THRESHOLD;
            if (isSoloSnare && previousWasSnare && isNotFastRepeat)
            {
                padHands[0] = (FourLaneDrumPad.RedDrum, Hand.RIGHT);
            }

            //Map each pad and hand to an animation
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
                YargLogger.LogFormatWarning("Unknown drum pad {0}, defaulting to right", pad);
                return Hand.RIGHT;
            }

            // Blue cymbal should stay right hand to prevent excessive twisting
            if (IsBlueCymbal(pad))
            {
                state.RecordHit(state.DefaultHand, noteTime);
                return state.DefaultHand;
            }

            var previousHitTime = state.LastHitTime.GetValueOrDefault(double.MaxValue);
            var isRepeatedNote = noteTime - previousHitTime <= REPEAT_THRESHOLD;
            var previousHand = state.LastUsedHand.GetValueOrDefault();
            var shouldAlternateHand = state.LastUsedHand.HasValue && isRepeatedNote;
            var hand = shouldAlternateHand ? SwitchHand(previousHand) : state.DefaultHand;
            state.RecordHit(hand, noteTime);
            return hand;
        }

        private static bool PreviousWasSnareHit(DrumNote previousNote)
        {
            if (previousNote is null)
            {
                return false;
            }

            foreach (var note in previousNote.AllNotes)
            {
                var pad = (FourLaneDrumPad) note.Pad;
                if (pad != FourLaneDrumPad.RedDrum && pad != FourLaneDrumPad.Kick)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBlueCymbal(FourLaneDrumPad pad)
        {
            return pad == FourLaneDrumPad.BlueCymbal;
        }

        private static Hand SwitchHand(Hand hand)
        {
            return hand == Hand.LEFT ? Hand.RIGHT : Hand.LEFT;
        }

        private AnimationEvent.AnimationType GetRandomCrashAnimation(Hand hand)
        {
            bool useCrash2 = _random.Next(2) == 0;
            return (useCrash2, hand) switch
            {
                (false, Hand.LEFT)  => AnimationEvent.AnimationType.Crash1LhHard,
                (false, Hand.RIGHT) => AnimationEvent.AnimationType.Crash1RhHard,
                (true,  Hand.LEFT)  => AnimationEvent.AnimationType.Crash2LhHard,
                (true,  Hand.RIGHT) => AnimationEvent.AnimationType.Crash2RhHard,
                _ => AnimationEvent.AnimationType.Crash1LhHard,
            };
        }

        private AnimationEvent.AnimationType GetAnimationType(FourLaneDrumPad pad, Hand hand)
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

                FourLaneDrumPad.GreenCymbal => GetRandomCrashAnimation(hand),

                FourLaneDrumPad.YellowDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom1LeftHand
                    : AnimationEvent.AnimationType.Tom1RightHand,

                FourLaneDrumPad.BlueDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.Tom2LeftHand
                    : AnimationEvent.AnimationType.Tom2RightHand,

                FourLaneDrumPad.GreenDrum => hand == Hand.LEFT
                    ? AnimationEvent.AnimationType.FloorTomLeftHand
                    : AnimationEvent.AnimationType.FloorTomRightHand,

                FourLaneDrumPad.Kick => AnimationEvent.AnimationType.Kick,

                _ => throw new ArgumentOutOfRangeException(nameof(pad), pad, "Unsupported drum pad for animation mapping."),
            };
        }
    }
}
