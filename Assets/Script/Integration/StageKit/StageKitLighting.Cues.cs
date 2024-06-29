using System;
using System.Collections.Generic;
using PlasticBand.Haptics;
using YARG.Core.Chart;
using YARG.Gameplay;
using Object = UnityEngine.Object;

namespace YARG.Integration.StageKit
{
    // Parent of primitives
    // Grandparent of cues
    public abstract class StageKitLighting
    {
        protected const byte NONE = 0b00000000;
        protected const byte ZERO = 0b00000001;
        protected const byte ONE = 0b00000010;
        protected const byte TWO = 0b00000100;
        protected const byte THREE = 0b00001000;
        protected const byte FOUR = 0b00010000;
        protected const byte FIVE = 0b00100000;
        protected const byte SIX = 0b01000000;
        protected const byte SEVEN = 0b10000000;
        protected const byte ALL = 0b11111111;

        [Flags]
        public enum ListenTypes
        {
            Next = 1,
            MajorBeat = 2,
            MinorBeat = 4,
            RedFretDrums = 8,
        }

        public virtual void Enable()
        {
        }

        public virtual void HandleLightingEvent(LightingType eventName)
        {
        }

        public virtual void HandleBeatlineEvent(BeatlineType eventName)
        {
        }

        public virtual void HandleDrumEvent(int eventName)
        {
        }

        public virtual void HandleVocalEvent(double eventName)
        {
        }

        public virtual void KillSelf()
        {
        }
    }

    // This is the parent class of all lighting cues. (not primitives)
    public abstract class StageKitLightingCue : StageKitLighting
    {
        protected const StageKitLedColor COLOR_NONE = StageKitLedColor.None;
        protected const StageKitLedColor BLUE = StageKitLedColor.Blue;
        protected const StageKitLedColor GREEN = StageKitLedColor.Green;
        protected const StageKitLedColor YELLOW = StageKitLedColor.Yellow;
        protected const StageKitLedColor RED = StageKitLedColor.Red;
        protected const StageKitLedColor COLOR_ALL = StageKitLedColor.All;

        public List<StageKitLighting> CuePrimitives = new();
        // While most cues only listen to events through their primitives, some cues listen directly to events so
        // We only want this switched on when enabled.
        public bool DirectListenEnabled;
    }

    public class BigRockEnding : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (RED, ALL),
            (RED, NONE),
            (RED, NONE),
            (RED, NONE),
        };
        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, ALL),
            (YELLOW, NONE),
        };
        private static readonly (StageKitLedColor, byte)[] PatternList3 =
        {
            (GREEN, NONE),
            (GREEN, ALL),
            (GREEN, NONE),
            (GREEN, NONE),
        };
        private static readonly (StageKitLedColor, byte)[] PatternList4 =
        {
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, ALL),
        };

        public BigRockEnding()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList3, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList4, 0.5f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(COLOR_ALL, ALL);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopWarm : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (RED, ZERO | FOUR),
            (RED, ONE | FIVE),
            (RED, TWO | SIX),
            (RED, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (YELLOW, TWO),
            (YELLOW, ONE),
            (YELLOW, ZERO),
            (YELLOW, SEVEN),
            (YELLOW, SIX),
            (YELLOW, FIVE),
            (YELLOW, FOUR),
            (YELLOW, THREE),
        };

        public LoopWarm()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopCool : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (BLUE, ZERO | FOUR),
            (BLUE, ONE | FIVE),
            (BLUE, TWO | SIX),
            (BLUE, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (GREEN, TWO),
            (GREEN, ONE),
            (GREEN, ZERO),
            (GREEN, SEVEN),
            (GREEN, SIX),
            (GREEN, FIVE),
            (GREEN, FOUR),
            (GREEN, THREE),
        };

        public LoopCool()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Harmony : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (YELLOW, THREE),
            (YELLOW, TWO),
            (YELLOW, ONE),
            (YELLOW, ZERO),
            (YELLOW, SEVEN),
            (YELLOW, SIX),
            (YELLOW, FIVE),
            (YELLOW, FOUR),
        };

        private static readonly (StageKitLedColor, byte)[] LargePatternList2 =
        {
            (RED, FOUR),
            (RED, THREE),
            (RED, TWO),
            (RED, ONE),
            (RED, ZERO),
            (RED, SEVEN),
            (RED, SIX),
            (RED, FIVE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (GREEN, FOUR),
            (GREEN, FIVE),
            (GREEN, SIX),
            (GREEN, SEVEN),
            (GREEN, ZERO),
            (GREEN, ONE),
            (GREEN, TWO),
            (GREEN, THREE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList2 =
        {
            (BLUE, FOUR),
            (BLUE, FIVE),
            (BLUE, SIX),
            (BLUE, SEVEN),
            (BLUE, ZERO),
            (BLUE, ONE),
            (BLUE, TWO),
            (BLUE, THREE),
        };

        public Harmony()
        {
            if (MasterLightingController.LargeVenue)
            {
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 4f));
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 4f));
            }
        }

        public override void Enable()
        {
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Sweep : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (RED, SIX | TWO),
            (RED, FIVE | ONE),
            (RED, FOUR | ZERO),
            (RED, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (YELLOW, SIX | TWO),
            (YELLOW, FIVE | ONE),
            (YELLOW, FOUR | ZERO),
            (YELLOW, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList2 =
        {
            (BLUE, ZERO),
            (BLUE, ONE),
            (BLUE, TWO),
            (BLUE, THREE),
            (BLUE, FOUR),
            (BLUE, NONE),
            (BLUE, NONE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList3 =
        {
            (GREEN, NONE),
            (GREEN, NONE),
            (GREEN, NONE),
            (GREEN, NONE),
            (GREEN, FOUR),
            (GREEN, THREE),
            (GREEN, TWO),
            (GREEN, ONE),
            (GREEN, ZERO),
        };

        public Sweep()
        {
            if (MasterLightingController.LargeVenue)
            {
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 4f));
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 2f));
            }
        }

        public override void Enable()
        {
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Frenzy : StageKitLightingCue
    {
        // Red off blue yellow
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (RED, ALL),
            (RED, NONE),
            (RED, NONE),
            (RED, NONE),
        };

        private static readonly (StageKitLedColor, byte)[] LargePatternList2 =
        {
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, ALL),
            (BLUE, NONE),
        };

        private static readonly (StageKitLedColor, byte)[] LargePatternList3 =
        {
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, ALL),
        };

        // Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (RED, NONE),
            (RED, ALL),
            (RED, ZERO | TWO | FOUR | SIX),
            (RED, ONE | THREE | FIVE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList2 =
        {
            (GREEN, NONE),
            (GREEN, NONE),
            (GREEN, ONE | THREE | FIVE | SEVEN),
            (GREEN, NONE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList3 =
        {
            (BLUE, ALL),
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, SIX | TWO),
        };

        public Frenzy()
        {
            if (MasterLightingController.LargeVenue)
            {
                // 4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f));
                CuePrimitives.Add(new BeatPattern(LargePatternList3, 1f));
            }
            else
            {
                // 4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 1f));
            }
        }

        public override void Enable()
        {
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class SearchLight : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (YELLOW, TWO),
            (YELLOW, THREE),
            (YELLOW, FOUR),
            (YELLOW, FIVE),
            (YELLOW, SIX),
            (YELLOW, SEVEN),
            (YELLOW, ZERO),
            (YELLOW, ONE),
        };

        private static readonly (StageKitLedColor, byte)[] LargePatternList2 =
        {
            (BLUE, ZERO),
            (BLUE, SEVEN),
            (BLUE, SIX),
            (BLUE, FIVE),
            (BLUE, FOUR),
            (BLUE, THREE),
            (BLUE, TWO),
            (BLUE, ONE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (YELLOW, ZERO),
            (YELLOW, SEVEN),
            (YELLOW, SIX),
            (YELLOW, FIVE),
            (YELLOW, FOUR),
            (YELLOW, THREE),
            (YELLOW, TWO),
            (YELLOW, ONE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList2 =
        {
            (RED, ZERO),
            (RED, SEVEN),
            (RED, SIX),
            (RED, FIVE),
            (RED, FOUR),
            (RED, THREE),
            (RED, TWO),
            (RED, ONE),
        };

        public SearchLight()
        {
            // 1 yellow@2 clockwise and 1 blue@0 counter clock.
            if (MasterLightingController.LargeVenue)
            {
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 2f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 2f));
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 2f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 2f));
            }
        }

        public override void Enable()
        {
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            }

            StageKitInterpreter.Instance.SetLed(GREEN, NONE);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Intro : StageKitLightingCue
    {
        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            StageKitInterpreter.Instance.SetLed(GREEN, ALL);
        }
    }

    public class FlareFast : StageKitLightingCue
    {
        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);

            if (StageKitInterpreter.PreviousLightingCue is ManualCool or LoopCool)
            {
                StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            }

            StageKitInterpreter.Instance.SetLed(BLUE, ALL);
        }
    }

    public class FlareSlow : StageKitLightingCue
    {
        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(COLOR_ALL, ALL);
        }
    }

    public class SilhouetteSpot : StageKitLightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals;

        public SilhouetteSpot()
        {
            if (StageKitInterpreter.PreviousLightingCue is Intro)
            {
                CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (BLUE, ALL) },
                    ListenTypes.RedFretDrums, true));
            }
        }

        public override void Enable()
        {
            if (StageKitInterpreter.PreviousLightingCue is Dischord)
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, ONE | THREE | FIVE | SEVEN);
                StageKitInterpreter.Instance.SetLed(GREEN, ALL);

                _enableBlueLedVocals = true;
            }
            else if (StageKitInterpreter.PreviousLightingCue is Stomp)
            {
                // Do nothing (for the chop suey ending at least)
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }

            DirectListenEnabled = true;
        }

        public override void HandleVocalEvent(double eventName)
        {
            if (!_enableBlueLedVocals) return;

            if (_blueOn)
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                _blueOn = false;
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(BLUE, ONE | THREE | FIVE | SEVEN);
                _blueOn = true;
            }

            _enableBlueLedVocals = false;
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (eventName != BeatlineType.Measure || StageKitInterpreter.PreviousLightingCue is not Dischord) return;
            if (StageKitInterpreter.PreviousLightingCue is not Dischord) return;
            _enableBlueLedVocals = true;
        }
    }

    public class Silhouettes : StageKitLightingCue
    {
        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
        }
    }

    public class Blackout : StageKitLightingCue
    {
        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
        }
    }

    public class ManualWarm : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (RED, ZERO | FOUR),
            (RED, ONE | FIVE),
            (RED, TWO | SIX),
            (RED, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (YELLOW, TWO),
            (YELLOW, ONE),
            (YELLOW, ZERO),
            (YELLOW, SEVEN),
            (YELLOW, SIX),
            (YELLOW, FIVE),
            (YELLOW, FOUR),
            (YELLOW, THREE),
        };

        public ManualWarm()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
            // I thought the Manuals listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            // new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ManualCool : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (BLUE, ZERO | FOUR),
            (BLUE, ONE | FIVE),
            (BLUE, TWO | SIX),
            (BLUE, THREE | SEVEN),
        };

        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (GREEN, TWO),
            (GREEN, ONE),
            (GREEN, ZERO),
            (GREEN, SEVEN),
            (GREEN, SIX),
            (GREEN, FIVE),
            (GREEN, FOUR),
            (GREEN, THREE),
        };

        public ManualCool()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 4f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Stomp : StageKitLightingCue
    {
        private bool _anythingOn;

        public override void Enable()
        {
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(BLUE, ALL);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            }

            StageKitInterpreter.Instance.SetLed(RED, ALL);
            StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            StageKitInterpreter.Instance.SetLed(YELLOW, ALL);

            _anythingOn = true;

            DirectListenEnabled = true;
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next) return;
            if (_anythingOn)
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }
            else
            {
                if (MasterLightingController.LargeVenue)
                {
                    StageKitInterpreter.Instance.SetLed(BLUE, ALL);
                }
                else
                {
                    StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                }

                StageKitInterpreter.Instance.SetLed(RED, ALL);
                StageKitInterpreter.Instance.SetLed(GREEN, ALL);
                StageKitInterpreter.Instance.SetLed(YELLOW, ALL);
            }

            _anythingOn = !_anythingOn;
        }
    }

    public class Dischord : StageKitLightingCue
    {
        private GameManager _gameManager;
        private float _currentPitch;
        private bool _greenIsSpinning;
        private bool _blueOnTwo = true;
        private readonly StageKitLighting _greenPattern;
        private byte _patternByte;
        private readonly BeatPattern _blueFour;
        private readonly BeatPattern _blueTwo;

        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (YELLOW, ZERO),
            (YELLOW, ONE),
            (YELLOW, TWO),
            (YELLOW, THREE),
            (YELLOW, FOUR),
            (YELLOW, FIVE),
            (YELLOW, SIX),
            (YELLOW, SEVEN),
        };
        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (GREEN, ZERO),
            (GREEN, SEVEN),
            (GREEN, SIX),
            (GREEN, FIVE),
            (GREEN, FOUR),
            (GREEN, THREE),
            (GREEN, TWO),
            (GREEN, ONE),
        };

        private static readonly (StageKitLedColor, byte)[] BlueFourPattern =
        {
            (BLUE, NONE),
            (BLUE, ZERO | TWO | FOUR | SIX),
        };

        private static readonly (StageKitLedColor, byte)[] BlueTwoPattern =
        {
            (BLUE, NONE),
            (BLUE, TWO | SIX),
        };

        public Dischord()
        {
            _greenIsSpinning = true;
            _greenPattern = new BeatPattern(PatternList2, 2f);
            _blueFour = new BeatPattern(BlueFourPattern, 4f, false);
            _blueTwo = new BeatPattern(BlueTwoPattern, 4f, false);
            CuePrimitives.Add(new ListenPattern(PatternList1, ListenTypes.MajorBeat | ListenTypes.MinorBeat));
            CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (RED, ALL) }, ListenTypes.RedFretDrums,
                true));
            CuePrimitives.Add(_blueTwo);
            CuePrimitives.Add(_blueFour);
            CuePrimitives.Add(_greenPattern);
        }

        public override void Enable()
        {
            _gameManager = Object.FindObjectOfType<GameManager>();
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, TWO | SIX);

            // Don't want to enable all, that turns on both blue patterns.
            CuePrimitives[0].Enable();
            CuePrimitives[1].Enable();
            _blueTwo.Enable();
            _greenPattern.Enable();

            DirectListenEnabled = true;
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next)
            {
                return;
            }

            if (_blueOnTwo)
            {
                _blueTwo.KillSelf();
                _blueFour.Enable();
                _blueOnTwo = false;
            }
            else
            {
                _blueFour.KillSelf();
                _blueTwo.Enable();
                _blueOnTwo = true;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (MasterLightingController.LargeVenue || eventName != BeatlineType.Measure) return;
            if (_greenIsSpinning)
            {
                _greenPattern.KillSelf();

                StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                _greenPattern.Enable();
            }

            _greenIsSpinning = !_greenIsSpinning;
        }
    }

    public class Default : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (BLUE, ALL),
            (RED, ALL),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (RED, ALL),
            (BLUE, ALL),
        };

        public Default()
        {
            if (MasterLightingController.LargeVenue)
            {
                CuePrimitives.Add(new ListenPattern(LargePatternList1, ListenTypes.Next));
            }
            else
            {
                CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (YELLOW, ALL) },
                    ListenTypes.RedFretDrums, true, true));
                CuePrimitives.Add(new ListenPattern(SmallPatternList1, ListenTypes.Next));
            }
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);

            if (!MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class MenuLighting : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] PatternList1 =
        {
            (BLUE, ZERO),
            (BLUE, ONE),
            (BLUE, TWO),
            (BLUE, THREE),
            (BLUE, FOUR),
            (BLUE, FIVE),
            (BLUE, SIX),
            (BLUE, SEVEN),
        };

        public MenuLighting()
        {
            CuePrimitives.Add(new TimedPattern(PatternList1, 2f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ScoreLighting : StageKitLightingCue
    {
        private static readonly (StageKitLedColor, byte)[] LargePatternList1 =
        {
            (RED, SIX | TWO),
            (RED, ONE | FIVE),
            (RED, ZERO | FOUR),
            (RED, SEVEN | THREE),
        };

        private static readonly (StageKitLedColor, byte)[] SmallPatternList1 =
        {
            (BLUE, ZERO),
            (BLUE, SEVEN),
            (BLUE, SIX),
            (BLUE, FIVE),
            (BLUE, FOUR),
            (BLUE, THREE),
            (BLUE, TWO),
            (BLUE, ONE),
        };

        private static readonly (StageKitLedColor, byte)[] PatternList2 =
        {
            (YELLOW, SIX | TWO),
            (YELLOW, SEVEN | THREE),
            (YELLOW, ZERO | FOUR),
            (YELLOW, ONE | FIVE),
        };

        public ScoreLighting()
        {
            if (MasterLightingController.LargeVenue)
            {
                CuePrimitives.Add(new TimedPattern(LargePatternList1, 1f));
            }
            else
            {
                CuePrimitives.Add(new TimedPattern(SmallPatternList1, 1f));
            }

            CuePrimitives.Add(new TimedPattern(PatternList2, 2f));
        }

        public override void Enable()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);

            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }
}
