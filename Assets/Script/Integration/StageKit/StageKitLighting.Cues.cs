using System;
using System.Collections.Generic;
using System.Threading;
using PlasticBand.Haptics;
using UnityEngine.Animations;
using YARG.Core.Chart;
using YARG.Gameplay;
using Object = UnityEngine.Object;

namespace YARG.Integration.StageKit
{
    //parent of primitives
    //grandparent of cues
    public abstract class StageKitLighting
    {
        public CancellationTokenSource CancellationTokenSource;

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

        public virtual void OnBeat()
        {
        }
    }

    //This is the parent class of all lighting cues. (not primitives)
    public abstract class StageKitLightingCue : StageKitLighting
    {
        protected const StageKitLedColor COLOR_NONE = StageKitLedColor.None;
        protected const StageKitLedColor BLUE = StageKitLedColor.Blue;
        protected const StageKitLedColor GREEN = StageKitLedColor.Green;
        protected const StageKitLedColor YELLOW = StageKitLedColor.Yellow;
        protected const StageKitLedColor RED = StageKitLedColor.Red;
        protected const StageKitLedColor COLOR_ALL = StageKitLedColor.All;

        //protected const int BLUE = 0;
        //protected const int GREEN = 1;
        //protected const int YELLOW = 2;
        //protected const int RED = 3;

        public List<StageKitLighting> CuePrimitives = new();
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
            StageKitInterpreter.Instance.SetLed(COLOR_ALL, ALL);

            CuePrimitives.Add(new BeatPattern(PatternList1, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList3, 0.5f));
            CuePrimitives.Add(new BeatPattern(PatternList4, 0.5f));
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
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);

            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
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
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);

            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
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
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 4f));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 4f));
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
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 4f));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 4f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 2f));
            }
        }
    }

    public class Frenzy : StageKitLightingCue
    {
        //red off blue yellow
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

        //Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

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
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                //4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f));
                CuePrimitives.Add(new BeatPattern(LargePatternList3, 1f));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                //4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 1f));
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
            //1 yellow@2 clockwise and 1 blue@0 counter clock.
            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 2f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 2f));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 2f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 2f));
            }

            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
        }
    }

    public class Intro : StageKitLightingCue
    {
        public Intro()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            StageKitInterpreter.Instance.SetLed(GREEN, ALL);
        }
    }

    public class FlareFast : StageKitLightingCue
    {
        public FlareFast()
        {
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);

            if (MasterLightingController.PreviousLightingCue.Type is LightingType.Cool_Manual
                or LightingType.Cool_Automatic)
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
        public FlareSlow()
        {
            StageKitInterpreter.Instance.SetLed(COLOR_ALL, ALL);
        }
    }

    public class SilhouetteSpot : StageKitLightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals = false;

        public SilhouetteSpot()
        {
            if (MasterLightingController.PreviousLightingCue.Type is LightingType.Dischord)
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, ONE | THREE | FIVE | SEVEN);
                StageKitInterpreter.Instance.SetLed(GREEN, ALL);

                _enableBlueLedVocals = true;
            }
            else if (MasterLightingController.PreviousLightingCue.Type is LightingType.Stomp)
            {
                //do nothing (for the chop suey ending at least)
            }
            else if (MasterLightingController.PreviousLightingCue.Type is LightingType.Intro)
            {
                CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (BLUE, ALL) },
                    ListenTypes.RedFretDrums, true));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                StageKitInterpreter.Instance.SetLed(GREEN, NONE);
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            }
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
            if (eventName != BeatlineType.Measure ||
                MasterLightingController.PreviousLightingCue.Type is not LightingType.Dischord)
                return;
            if (MasterLightingController.PreviousLightingCue.Type is not LightingType.Dischord) return;
            _enableBlueLedVocals = true;
        }
    }

    public class Silhouettes : StageKitLightingCue
    {
        public Silhouettes()
        {
            StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
        }
    }

    public class Blackout : StageKitLightingCue
    {
        public Blackout()
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
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 8f));
            // I thought it listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
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
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            CuePrimitives.Add(new BeatPattern(PatternList1, 4f));
            CuePrimitives.Add(new BeatPattern(PatternList2, 4f));
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }
    }

    public class Stomp : StageKitLightingCue
    {
        private bool _anythingOn;

        public Stomp()
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
        private readonly GameManager _gameManager = Object.FindObjectOfType<GameManager>();
        private float _currentPitch;
        private bool _greenIsSpinning = false;
        private bool _blueOnTwo = true;
        private StageKitLighting _greenPattern;
        private byte _patternByte;
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

        public Dischord()
        {
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            CuePrimitives.Add(new ListenPattern(PatternList1, ListenTypes.MajorBeat | ListenTypes.MinorBeat));
            _greenPattern = new BeatPattern(PatternList2, 2f);
            CuePrimitives.Add(_greenPattern);
            _greenIsSpinning = true;
            CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (RED, ALL) }, ListenTypes.RedFretDrums,
                true));
            StageKitInterpreter.Instance.SetLed(BLUE, TWO | SIX);
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next) return;

            if (_blueOnTwo)
            {
                CuePrimitives.Add(new BeatPattern(
                    new (StageKitLedColor, byte)[] { (BLUE, NONE), (BLUE, ZERO | TWO | FOUR | SIX) },
                    4f, false));
                _blueOnTwo = false;
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(new (StageKitLedColor, byte)[] { (BLUE, NONE), (BLUE, TWO | SIX) },
                    4f, false));
                _blueOnTwo = true;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (MasterLightingController.LargeVenue || eventName != BeatlineType.Measure) return;
            if (_greenIsSpinning)
            {
                _gameManager.BeatEventHandler.Unsubscribe(_greenPattern.OnBeat);
                StageKitInterpreter.Instance.CurrentLightingCue.CuePrimitives.Remove(_greenPattern);

                StageKitInterpreter.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                _greenPattern = new BeatPattern(PatternList2, 2f);
                CuePrimitives.Add(_greenPattern);
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
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            StageKitInterpreter.Instance.SetLed(BLUE, NONE);

            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
                CuePrimitives.Add(new ListenPattern(LargePatternList1, ListenTypes.Next));
            }
            else
            {
                CuePrimitives.Add(new ListenPattern(new (StageKitLedColor, byte)[] { (YELLOW, ALL) },
                    ListenTypes.RedFretDrums, true,
                    true));
                CuePrimitives.Add(new ListenPattern(SmallPatternList1, ListenTypes.Next));
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
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);
            StageKitInterpreter.Instance.SetLed(RED, NONE);
            StageKitInterpreter.Instance.SetLed(YELLOW, NONE);
            CuePrimitives.Add(new TimedPattern(PatternList1, 2f));
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
            StageKitInterpreter.Instance.SetLed(GREEN, NONE);

            if (MasterLightingController.LargeVenue)
            {
                StageKitInterpreter.Instance.SetLed(BLUE, NONE);
                CuePrimitives.Add(new TimedPattern(LargePatternList1, 1f));
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new TimedPattern(SmallPatternList1, 1f));
            }

            CuePrimitives.Add(new TimedPattern(PatternList2, 2f));
        }
    }
}
/*
    "I think a good novel would be where a bunch of men on a ship are looking for a whale. They look and look, but you
    know what? They never find him. And you know why they never find him? It doesn't say. The book leaves it up to you,
    the reader, to decide. Then, at the very end, there's a page that can lick and it tastes like Kool-Aid."

    - Jack Handey
*/