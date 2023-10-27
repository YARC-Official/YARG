using YARG.Core.Chart;
using UnityEngine;
using YARG.Gameplay;

namespace YARG.Integration.StageKit
{
    public class BigRockEnding : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
        {
            (RED, ALL),
            (RED, NONE),
            (RED, NONE),
            (RED, NONE),
        };
        private static readonly (int, byte)[] PatternList2 =
        {
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, ALL),
            (YELLOW, NONE),
        };
        private static readonly (int, byte)[] PatternList3 =
        {
            (GREEN, NONE),
            (GREEN, ALL),
            (GREEN, NONE),
            (GREEN, NONE),
        };
        private static readonly (int, byte)[] PatternList4 =
        {
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, ALL),
        };

        public BigRockEnding()
        {
            StageKitLightingController.Instance.SetLed(RED, ALL);
            StageKitLightingController.Instance.SetLed(BLUE, ALL);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
            StageKitLightingController.Instance.SetLed(YELLOW, ALL);

            CuePrimitives.Add(new BeatPattern(PatternList1, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(PatternList2, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(PatternList3, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(PatternList4, true, 8.0f));
        }
    }

    public class LoopWarm : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
        {
            (RED, ZERO | FOUR),
            (RED, ONE | FIVE),
            (RED, TWO | SIX),
            (RED, THREE | SEVEN),
        };

        private static readonly (int, byte)[] PatternList2 =
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
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);

            CuePrimitives.Add(new BeatPattern(PatternList1));
            CuePrimitives.Add(new BeatPattern(PatternList2, true, 0.5f));
        }
    }

    public class LoopCool : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
        {
            (BLUE, ZERO | FOUR),
            (BLUE, ONE | FIVE),
            (BLUE, TWO | SIX),
            (BLUE, THREE | SEVEN),
        };

        private static readonly (int, byte)[] PatternList2 =
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
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);

            CuePrimitives.Add(new BeatPattern(PatternList1));
            CuePrimitives.Add(new BeatPattern(PatternList2, true, 0.5f));
        }
    }

    public class Harmony : StageKitLightingCue
    {
        private static readonly (int, byte)[] LargePatternList1 =
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

        private static readonly (int, byte)[] LargePatternList2 =
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

        private static readonly (int, byte)[] SmallPatternList1 =
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

        private static readonly (int, byte)[] SmallPatternList2 =
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
            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1));
                CuePrimitives.Add(new BeatPattern(LargePatternList2));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2));
            }
        }
    }

    public class Sweep : StageKitLightingCue
    {
        private static readonly (int, byte)[] LargePatternList1 =
        {
            (RED, SIX | TWO),
            (RED, FIVE | ONE),
            (RED, FOUR | ZERO),
            (RED, THREE | SEVEN),
        };

        private static readonly (int, byte)[] SmallPatternList1 =
        {
            (YELLOW, SIX | TWO),
            (YELLOW, FIVE | ONE),
            (YELLOW, FOUR | ZERO),
            (YELLOW, THREE | SEVEN),
        };

        private static readonly (int, byte)[] SmallPatternList2 =
        {
            (BLUE, ZERO),
            (BLUE, ONE),
            (BLUE, TWO),
            (BLUE, THREE),
            (BLUE, FOUR),
            (BLUE, NONE),
            (BLUE, NONE),
        };

        private static readonly (int, byte)[] SmallPatternList3 =
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
            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, true, 2.0f));
            }
        }
    }

    public class Frenzy : StageKitLightingCue
    {
        //red off blue yellow
        private static readonly (int, byte)[] LargePatternList1 =
        {
            (RED, ALL),
            (RED, NONE),
            (RED, NONE),
            (RED, NONE),
        };

        private static readonly (int, byte)[] LargePatternList2 =
        {
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, ALL),
            (BLUE, NONE),
        };

        private static readonly (int, byte)[] LargePatternList3 =
        {
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, NONE),
            (YELLOW, ALL),
        };

        //Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

        private static readonly (int, byte)[] SmallPatternList1 =
        {
            (RED, NONE),
            (RED, ALL),
            (RED, ZERO | TWO | FOUR | SIX),
            (RED, ONE | THREE | FIVE | SEVEN),
        };

        private static readonly (int, byte)[] SmallPatternList2 =
        {
            (GREEN, NONE),
            (GREEN, NONE),
            (GREEN, ONE | THREE | FIVE | SEVEN),
            (GREEN, NONE),
        };

        private static readonly (int, byte)[] SmallPatternList3 =
        {
            (BLUE, ALL),
            (BLUE, NONE),
            (BLUE, NONE),
            (BLUE, SIX | TWO),
        };

        public Frenzy()
        {
            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                //4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(LargePatternList1, true, 4.0f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, true, 4.0f));
                CuePrimitives.Add(new BeatPattern(LargePatternList3, true, 4.0f));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                //4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, true, 4.0f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, true, 4.0f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, true, 4.0f));
            }
        }
    }

    public class SearchLight : StageKitLightingCue
    {
        private static readonly (int, byte)[] LargePatternList1 =
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

        private static readonly (int, byte)[] LargePatternList2 =
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

        private static readonly (int, byte)[] SmallPatternList1 =
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

        private static readonly (int, byte)[] SmallPatternList2 =
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
            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, true, 2.0f));
                CuePrimitives.Add(new BeatPattern(LargePatternList2, true, 2.0f));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, true, 2.0f));
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, true, 2.0f));
            }

            StageKitLightingController.Instance.SetLed(GREEN, NONE);
        }
    }

    public class Intro : StageKitLightingCue
    {
        public Intro()
        {
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
        }
    }

    public class FlareFast : StageKitLightingCue
    {
        public FlareFast()
        {
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);

            if (StageKitLightingController.Instance.PreviousLightingCue is ManualCool or LoopCool)
            {
                StageKitLightingController.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
            }

            StageKitLightingController.Instance.SetLed(BLUE, ALL);
        }
    }

    public class FlareSlow : StageKitLightingCue
    {
        public FlareSlow()
        {
            StageKitLightingController.Instance.SetLed(BLUE, ALL);
            StageKitLightingController.Instance.SetLed(YELLOW, ALL);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
            StageKitLightingController.Instance.SetLed(RED, ALL);
        }
    }

    public class SilhouetteSpot : StageKitLightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals = false;

        public SilhouetteSpot()
        {
            if (StageKitLightingController.Instance.PreviousLightingCue is Dischord)
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, ONE | THREE | FIVE | SEVEN);
                StageKitLightingController.Instance.SetLed(GREEN, ALL);

                _enableBlueLedVocals = true;
            }
            else if (StageKitLightingController.Instance.PreviousLightingCue is Stomp)
            {
                //do nothing (for the chop suey ending at least)
            }
            else if (StageKitLightingController.Instance.PreviousLightingCue is Intro)
            {
                CuePrimitives.Add(new ListenPattern(new (int, byte)[] { (BLUE, ALL) }, ListenTypes.RedFretDrums, true));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            }
        }

        public override void HandleVocalEvent(double eventName)
        {
            if (!_enableBlueLedVocals) return;

            if (_blueOn)
            {
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                _blueOn = false;
            }
            else
            {
                StageKitLightingController.Instance.SetLed(BLUE, ONE | THREE | FIVE | SEVEN);
                _blueOn = true;
            }

            _enableBlueLedVocals = false;
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (eventName != BeatlineType.Measure ||
                StageKitLightingController.Instance.PreviousLightingCue is not Dischord)
                return;
            if (StageKitLightingController.Instance.PreviousLightingCue is not Dischord) return;
            _enableBlueLedVocals = true;
        }
    }

    public class Silhouettes : StageKitLightingCue
    {
        public Silhouettes()
        {
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
        }
    }

    public class Blackout : StageKitLightingCue
    {
        public Blackout()
        {
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
        }
    }

    public class ManualWarm : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
        {
            (RED, ZERO | FOUR),
            (RED, ONE | FIVE),
            (RED, TWO | SIX),
            (RED, THREE | SEVEN),
        };

        private static readonly (int, byte)[] PatternList2 =
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
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            CuePrimitives.Add(new BeatPattern(PatternList1));
            CuePrimitives.Add(new BeatPattern(PatternList2, true, 0.5f));
            // I thought it listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }
    }

    public class ManualCool : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
        {
            (BLUE, ZERO | FOUR),
            (BLUE, ONE | FIVE),
            (BLUE, TWO | SIX),
            (BLUE, THREE | SEVEN),
        };

        private static readonly (int, byte)[] PatternList2 =
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
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            CuePrimitives.Add(new BeatPattern(PatternList1));
            CuePrimitives.Add(new BeatPattern(PatternList2));
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }
    }

    public class Stomp : StageKitLightingCue
    {
        private bool _anythingOn;

        public Stomp()
        {
            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(BLUE, ALL);
            }
            else
            {
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
            }

            StageKitLightingController.Instance.SetLed(RED, ALL);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
            StageKitLightingController.Instance.SetLed(YELLOW, ALL);

            _anythingOn = true;
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next) return;
            if (_anythingOn)
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            }
            else
            {
                if (StageKitLightingController.Instance.LargeVenue)
                {
                    StageKitLightingController.Instance.SetLed(BLUE, ALL);
                }
                else
                {
                    StageKitLightingController.Instance.SetLed(BLUE, NONE);
                }

                StageKitLightingController.Instance.SetLed(RED, ALL);
                StageKitLightingController.Instance.SetLed(GREEN, ALL);
                StageKitLightingController.Instance.SetLed(YELLOW, ALL);
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
        private static readonly (int, byte)[] PatternList1 =
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
        private static readonly (int, byte)[] PatternList2 =
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
            StageKitLightingController.Instance.SetLed(RED, NONE);
            CuePrimitives.Add(new ListenPattern(PatternList1, ListenTypes.MajorBeat | ListenTypes.MinorBeat));
            _greenPattern = new BeatPattern(PatternList2, true, 2.0f);
            CuePrimitives.Add(_greenPattern);
            _greenIsSpinning = true;
            CuePrimitives.Add(new ListenPattern(new (int, byte)[] { (RED, ALL) }, ListenTypes.RedFretDrums, true));
            StageKitLightingController.Instance.SetLed(BLUE, TWO | SIX);
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next) return;

            if (_blueOnTwo)
            {
                CuePrimitives.Add(new BeatPattern(new (int, byte)[] { (BLUE, NONE), (BLUE, ZERO | TWO | FOUR | SIX) },
                    false));
                _blueOnTwo = false;
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(new (int, byte)[] { (BLUE, NONE), (BLUE, TWO | SIX) }, false));
                _blueOnTwo = true;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (StageKitLightingController.Instance.LargeVenue || eventName != BeatlineType.Measure) return;
            if (_greenIsSpinning)
            {
                _gameManager.BeatEventManager.Unsubscribe(_greenPattern.OnBeat);
                StageKitLightingController.Instance.CurrentLightingCue.CuePrimitives.Remove(_greenPattern);

                StageKitLightingController.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                _greenPattern = new BeatPattern(PatternList2, true, 2.0f);
                CuePrimitives.Add(_greenPattern);
            }

            _greenIsSpinning = !_greenIsSpinning;
        }
    }

    public class Default : StageKitLightingCue
    {
        private static readonly (int, byte)[] LargePatternList1 =
        {
            (BLUE, ALL),
            (RED, ALL),
        };

        private static readonly (int, byte)[] SmallPatternList1 =
        {
            (RED, ALL),
            (BLUE, ALL),
        };

        public Default()
        {
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);

            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                CuePrimitives.Add(new ListenPattern(LargePatternList1, ListenTypes.Next));
            }
            else
            {
                CuePrimitives.Add(new ListenPattern(new (int, byte)[] { (YELLOW, ALL) }, ListenTypes.RedFretDrums, true,
                    true));
                CuePrimitives.Add(new ListenPattern(SmallPatternList1, ListenTypes.Next));
            }
        }
    }

    public class MenuLighting : StageKitLightingCue
    {
        private static readonly (int, byte)[] PatternList1 =
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
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            CuePrimitives.Add(new TimedPattern(PatternList1, 2f));
        }
    }

    public class ScoreLighting : StageKitLightingCue
    {
        private static readonly (int, byte)[] LargePatternList1 =
        {
            (RED, SIX | TWO),
            (RED, ONE | FIVE),
            (RED, ZERO | FOUR),
            (RED, SEVEN | THREE),
        };

        private static readonly (int, byte)[] SmallPatternList1 =
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

        private static readonly (int, byte)[] PatternList2 =
        {
            (YELLOW, SIX | TWO),
            (YELLOW, SEVEN | THREE),
            (YELLOW, ZERO | FOUR),
            (YELLOW, ONE | FIVE),
        };

        public ScoreLighting()
        {
            StageKitLightingController.Instance.SetLed(GREEN, NONE);

            if (StageKitLightingController.Instance.LargeVenue)
            {
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                CuePrimitives.Add(new TimedPattern(LargePatternList1, 1f));
            }
            else
            {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                CuePrimitives.Add(new TimedPattern(SmallPatternList1, 1f));
            }

            CuePrimitives.Add(new TimedPattern(PatternList2, 2f));
        }
    }
}