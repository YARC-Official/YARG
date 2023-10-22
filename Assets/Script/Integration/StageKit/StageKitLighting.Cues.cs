using System.Collections.Generic;
using YARG.Core.Chart;
using UnityEngine;
using YARG.Gameplay;

namespace YARG.Integration.StageKit
{
    internal class BigRockEnding : StageKitLightingCue
    {
        public BigRockEnding()
        {
            StageKitLightingController.Instance.SetLed(RED, ALL);
            StageKitLightingController.Instance.SetLed(BLUE, ALL);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
            StageKitLightingController.Instance.SetLed(YELLOW, ALL);
            var patternList1 = new List<(int, byte)>
            {
                (RED, ALL),
                (RED, NONE),
                (RED, NONE),
                (RED, NONE),
            };
            var patternList2 = new List<(int, byte)>
            {
                (YELLOW, NONE),
                (YELLOW, NONE),
                (YELLOW, ALL),
                (YELLOW, NONE),
            };
            var patternList3 = new List<(int, byte)>
            {
                (GREEN, NONE),
                (GREEN, ALL),
                (GREEN, NONE),
                (GREEN, NONE),
            };
            var patternList4 = new List<(int, byte)>
            {
                (BLUE, NONE),
                (BLUE, NONE),
                (BLUE, NONE),
                (BLUE, ALL),
            };
            CuePrimitives.Add(new BeatPattern(patternList1, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(patternList2, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(patternList3, true, 8.0f));
            CuePrimitives.Add(new BeatPattern(patternList4, true, 8.0f));
        }

    }
    internal class LoopWarm : StageKitLightingCue
    {
        public LoopWarm()
        {
            StageKitLightingController.Instance.SetLed(GREEN,NONE);
            StageKitLightingController.Instance.SetLed(BLUE,NONE);

            var patternList1 = new List<(int, byte)>
            {
                (RED, ZERO | FOUR),
                (RED, ONE | FIVE),
                (RED, TWO | SIX),
                (RED, THREE | SEVEN),
            };

            var patternList2 = new List<(int, byte)>
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

            CuePrimitives.Add( new BeatPattern(patternList1));
            CuePrimitives.Add( new BeatPattern(patternList2, true, 0.5f));
        }
    }
    internal class LoopCool : StageKitLightingCue
    {
        public LoopCool()
        {
            StageKitLightingController.Instance.SetLed(YELLOW,NONE);
            StageKitLightingController.Instance.SetLed(RED,NONE);

            var patternList1 = new List<(int, byte)>
            {
                (BLUE, ZERO | FOUR),
                (BLUE, ONE | FIVE),
                (BLUE, TWO | SIX),
                (BLUE, THREE | SEVEN),
            };

            var patternList2 = new List<(int, byte)>
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

            CuePrimitives.Add( new BeatPattern(patternList1));
            CuePrimitives.Add( new BeatPattern(patternList2,true, 0.5f));
        }
    }
    internal class Harmony : StageKitLightingCue
    {
        public Harmony()
        {
            List<(int, byte)> patternList2;
            List<(int, byte)> patternList1;

            if (LargeVenue)
            {
                patternList1 = new List<(int, byte)>
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

                patternList2 = new List<(int, byte)>
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
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
            }
            else
            {
                patternList1 = new List<(int, byte)>
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

                patternList2 = new List<(int, byte)>
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

                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);

            }
            CuePrimitives.Add( new BeatPattern(patternList1));
            CuePrimitives.Add( new BeatPattern(patternList2));
        }
    }
    internal class Sweep : StageKitLightingCue
    {
        public Sweep()
        {
            List<(int, byte)> patternList1;

            if  (LargeVenue)
            {
                patternList1 = new List<(int, byte)>
                {
                    (RED, SIX | TWO), (RED, FIVE | ONE), (RED, FOUR | ZERO), (RED, THREE | SEVEN),
                };

                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                CuePrimitives.Add( new BeatPattern(patternList1));
            }
            else
            {
                patternList1 = new List<(int, byte)>
                {
                    (YELLOW, SIX | TWO), (YELLOW, FIVE | ONE), (YELLOW, FOUR | ZERO), (YELLOW, THREE | SEVEN),
                };
                var patternList2 = new List<(int, byte)>
                {
                    (BLUE, ZERO),
                    (BLUE, ONE),
                    (BLUE, TWO),
                    (BLUE, THREE),
                    (BLUE, FOUR),
                    (BLUE, NONE),
                    (BLUE, NONE),
                };
                var patternList3 = new List<(int, byte)>
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

                StageKitLightingController.Instance.SetLed(RED, NONE);
                CuePrimitives.Add( new BeatPattern(patternList1));
                CuePrimitives.Add( new BeatPattern(patternList2));
                CuePrimitives.Add( new BeatPattern(patternList3, true, 2.0f));
            }
        }
    }
	internal class Frenzy : StageKitLightingCue
    {
        public Frenzy() {
            List<(int, byte)> patternList3;
            List<(int, byte)> patternList2;
            List<(int, byte)> patternList1;
            if  (LargeVenue)//red off blue yellow
            {
                patternList1 = new List<(int, byte)>
                {
                    (RED, ALL), (RED, NONE), (RED, NONE), (RED, NONE),
                };

                patternList2 = new List<(int, byte)>
                {
                    (BLUE, NONE), (BLUE, NONE), (BLUE, ALL), (BLUE, NONE),
                };

                patternList3 = new List<(int, byte)>
                {
                    (YELLOW, NONE), (YELLOW, NONE), (YELLOW, NONE), (YELLOW, ALL),
                };

                StageKitLightingController.Instance.SetLed(GREEN, NONE);
            }
            else
            {
                //Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

                patternList1 = new List<(int, byte)> {
                    (RED, NONE),
                    (RED,ALL),
                    (RED, ZERO | TWO | FOUR | SIX),
                    (RED, ONE| THREE | FIVE | SEVEN),
                };

                patternList2 = new List<(int, byte)> {
                    (GREEN, NONE),
                    (GREEN, NONE),
                    (GREEN, ONE| THREE | FIVE | SEVEN),
                    (GREEN, NONE),
                };

                patternList3 = new List<(int, byte)> {
                    (BLUE, ALL),
                    (BLUE, NONE),
                    (BLUE, NONE),
                    (BLUE, SIX |  TWO),
                };
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            }
            CuePrimitives.Add( new BeatPattern(patternList1,true, 4.0f)); //4 times a beats to control on and off because of the 2 different patterns on one color
            CuePrimitives.Add( new BeatPattern(patternList2,true, 4.0f));
            CuePrimitives.Add(new BeatPattern(patternList3,true, 4.0f));
        }
	}
	internal class SearchLight : StageKitLightingCue
    {
        private byte _secondLed;
        private int _secondColor;

        public SearchLight() {
            List<(int, byte)> patternList2;
            List<(int, byte)> patternList1;
            //1 yellow@2 clockwise and 1 blue@0 counter clock.
            if  (LargeVenue)
            {
                patternList1 = new List<(int, byte)>
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
                patternList2 = new List<(int, byte)>
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
                StageKitLightingController.Instance.SetLed(RED, NONE);
            }
            else
            {
                patternList1 = new List<(int, byte)> {
                    (YELLOW, ZERO),
                    (YELLOW, SEVEN),
                    (YELLOW, SIX),
                    (YELLOW, FIVE),
                    (YELLOW, FOUR),
                    (YELLOW, THREE),
                    (YELLOW, TWO),
                    (YELLOW, ONE),
                };

                patternList2 = new List<(int, byte)> {
                    (RED, ZERO),
                    (RED, SEVEN),
                    (RED, SIX),
                    (RED, FIVE),
                    (RED, FOUR),
                    (RED, THREE),
                    (RED, TWO),
                    (RED, ONE),
                };
                StageKitLightingController.Instance.SetLed(BLUE, NONE);

            }
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            CuePrimitives.Add(new BeatPattern(patternList1,true,2.0f));
            CuePrimitives.Add(new BeatPattern(patternList2,true,2.0f));

		}
	}
    internal class Intro : StageKitLightingCue
    {
        public Intro()
        {
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            StageKitLightingController.Instance.SetLed(GREEN, ALL);
        }
    }
	internal class FlareFast : StageKitLightingCue
    {
        public FlareFast() {
            StageKitLightingController.Instance.SetLed(YELLOW,NONE);
            StageKitLightingController.Instance.SetLed(RED,NONE);

            if (PreviousLightingCue is ManualCool or LoopCool)
            {
                StageKitLightingController.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                StageKitLightingController.Instance.SetLed(GREEN,NONE);
            }

            StageKitLightingController.Instance.SetLed(BLUE,ALL);
        }
	}
    internal class FlareSlow : StageKitLightingCue
    {
        public FlareSlow() {
            StageKitLightingController.Instance.SetLed(BLUE,ALL);
            StageKitLightingController.Instance.SetLed(YELLOW,ALL);
            StageKitLightingController.Instance.SetLed(GREEN,ALL);
            StageKitLightingController.Instance.SetLed(RED,ALL);
        }
    }
	internal class SilhouetteSpot : StageKitLightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals = false;

		public SilhouetteSpot()
        {
            if (PreviousLightingCue is Dischord)
            {
				StageKitLightingController.Instance.SetLed(RED,NONE);
				StageKitLightingController.Instance.SetLed(YELLOW,NONE);
				StageKitLightingController.Instance.SetLed(BLUE,ONE|THREE|FIVE|SEVEN);
				StageKitLightingController.Instance.SetLed(GREEN,ALL);

                _enableBlueLedVocals = true;
			}
            else if (PreviousLightingCue is Stomp)
            {
                //do nothing (for the chop suey ending at least)
			}
            else if (PreviousLightingCue is Intro)
            {
                CuePrimitives.Add(new ListenPattern(new List<(int, byte)>{(BLUE, ALL)}, ListenTypes.RedFretDrums,true));
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

            if (_blueOn) {
                StageKitLightingController.Instance.SetLed(BLUE,NONE);
                _blueOn = false;
            } else {
                StageKitLightingController.Instance.SetLed(BLUE,ONE|THREE|FIVE|SEVEN);
                _blueOn = true;
            }
            _enableBlueLedVocals = false;
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (eventName != BeatlineType.Measure || PreviousLightingCue is not Dischord) return;
            if (PreviousLightingCue is not Dischord) return;
            _enableBlueLedVocals = true;
        }
	}
	internal class Silhouettes: StageKitLightingCue
    {
		public Silhouettes()
        {
			StageKitLightingController.Instance.SetLed(GREEN,ALL);
			StageKitLightingController.Instance.SetLed(YELLOW,NONE);
			StageKitLightingController.Instance.SetLed(BLUE,NONE);
			StageKitLightingController.Instance.SetLed(RED,NONE);
		}
	}
	internal class Blackout : StageKitLightingCue
    {
		public Blackout()
        {
            StageKitLightingController.Instance.SetLed(GREEN,NONE);
			StageKitLightingController.Instance.SetLed(YELLOW,NONE);
			StageKitLightingController.Instance.SetLed(BLUE,NONE);
			StageKitLightingController.Instance.SetLed(RED,NONE);
		}
	}
	internal class ManualWarm : StageKitLightingCue
    {
        public ManualWarm()
        {
            StageKitLightingController.Instance.SetLed(GREEN,NONE);
            StageKitLightingController.Instance.SetLed(BLUE,NONE);

            var patternList1 = new List<(int, byte)>
            {
                (RED, ZERO | FOUR),
                (RED, ONE | FIVE),
                (RED, TWO | SIX),
                (RED, THREE | SEVEN),
            };
           CuePrimitives.Add(new BeatPattern(patternList1));

            var patternList2 = new List<(int, byte)>
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

            CuePrimitives.Add(new BeatPattern(patternList2, true, 0.5f));
            // I thought it listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }
	}
	internal class ManualCool : StageKitLightingCue{
        public ManualCool()
        {
            StageKitLightingController.Instance.SetLed(YELLOW,NONE);
            StageKitLightingController.Instance.SetLed(RED,NONE);
            var patternList1 = new List<(int, byte)>
            {
                (BLUE, ZERO | FOUR),
                (BLUE, ONE | FIVE),
                (BLUE, TWO | SIX),
                (BLUE, THREE | SEVEN),
            };
            CuePrimitives.Add(new BeatPattern(patternList1));

            var patternList2 = new List<(int, byte)>
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
            CuePrimitives.Add(new BeatPattern(patternList2));
            //new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);
        }
	}
	internal class Stomp : StageKitLightingCue{

		private bool _anythingOn;

		public Stomp()
        {
            if  (LargeVenue)
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

        public override void HandleLightingEvent(LightingType eventName) {
            if (eventName != LightingType.Keyframe_Next) return;
			if (_anythingOn) {
                StageKitLightingController.Instance.SetLed(RED, NONE);
                StageKitLightingController.Instance.SetLed(GREEN, NONE);
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            } else {
				if (LargeVenue) {
                    StageKitLightingController.Instance.SetLed(BLUE, ALL);
				} else {
                    StageKitLightingController.Instance.SetLed(BLUE, NONE);
				}
                StageKitLightingController.Instance.SetLed(RED, ALL);
                StageKitLightingController.Instance.SetLed(GREEN, ALL);
                StageKitLightingController.Instance.SetLed(YELLOW, ALL);
			}
			_anythingOn = !_anythingOn;
		}
	}
	internal class Dischord : StageKitLightingCue
    {
        private readonly GameManager _gameManager = Object.FindObjectOfType<GameManager>();
		private float _currentPitch;
        private bool _greenIsSpinning = false;
        private bool _blueOnTwo = true;
        private StageKitLighting _greenPattern;
        private byte _patternByte;
        private readonly List<(int, byte)> _patternList2;

		public Dischord()
        {
			StageKitLightingController.Instance.SetLed(RED,NONE);
			var patternList1 = new List<(int, byte)>
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
            CuePrimitives.Add(new ListenPattern(patternList1, ListenTypes.MajorBeat | ListenTypes.MinorBeat));

            _patternList2 = new List<(int, byte)>
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
            _greenPattern = new BeatPattern(_patternList2, true, 2.0f);
            CuePrimitives.Add(_greenPattern);
            _greenIsSpinning = true;
            CuePrimitives.Add(new ListenPattern(new List<(int, byte)> { (RED, ALL) }, ListenTypes.RedFretDrums, true));
            StageKitLightingController.Instance.SetLed(BLUE, TWO | SIX);
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (eventName != LightingType.Keyframe_Next) return;

            if (_blueOnTwo)
            {
                CuePrimitives.Add(new BeatPattern(new List<(int, byte)>{ (BLUE, NONE), (BLUE, ZERO | TWO | FOUR | SIX) }, false ));
                _blueOnTwo = false;
            }
            else
            {
                CuePrimitives.Add(new BeatPattern(new List<(int, byte)>{ (BLUE, NONE), (BLUE, TWO | SIX) }, false));
                _blueOnTwo = true;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (LargeVenue || eventName != BeatlineType.Measure) return;
            if (_greenIsSpinning)
            {
                _gameManager.BeatEventManager.Unsubscribe(_greenPattern.OnBeat);
                StageKitLightingController.Instance.CurrentLightingCue.CuePrimitives.Remove(_greenPattern);

                StageKitLightingController.Instance.SetLed(GREEN, ALL);
            }
            else
            {
                _greenPattern = new BeatPattern(_patternList2, true, 2.0f);
                CuePrimitives.Add(_greenPattern);
            }
            _greenIsSpinning = !_greenIsSpinning;
        }
    }
	internal class Default : StageKitLightingCue
    {
        public Default()
        {
            List<(int, byte)> patternList1;
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            if  (LargeVenue)
            {
                patternList1 = new List<(int, byte)>
                {
                    (BLUE, ALL),
                    (RED, ALL),
                } ;
                StageKitLightingController.Instance.SetLed(YELLOW, NONE);
            }
            else
            {
                patternList1 = new List<(int, byte)>
                {
                    (RED, ALL),
                    (BLUE, ALL),
                } ;
                CuePrimitives.Add(new ListenPattern(new List<(int, byte)>{(YELLOW, ALL)}, ListenTypes.RedFretDrums, true,true));
            }
            CuePrimitives.Add(new ListenPattern(patternList1, ListenTypes.Next));
        }
	}
    internal class MenuLighting : StageKitLightingCue{
		public MenuLighting()
        {
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
			var patternList = new List<(int, byte)>
            {
				(BLUE, ZERO),
				(BLUE, ONE),
				(BLUE, TWO),
				(BLUE, THREE),
				(BLUE, FOUR),
				(BLUE, FIVE),
				(BLUE, SIX),
				(BLUE, SEVEN),
			} ;
            CuePrimitives.Add(new TimedPattern(patternList, 2f));
		}
	}
	internal class ScoreLighting : StageKitLightingCue
    {
        public ScoreLighting()
        {
            List<(int, byte)> patternList1;
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            if  (LargeVenue)
            {
                patternList1 = new List<(int, byte)>
                {
                    (RED, SIX | TWO),
                    (RED, ONE | FIVE),
                    (RED, ZERO | FOUR),
                    (RED, SEVEN | THREE),
                };
                StageKitLightingController.Instance.SetLed(BLUE, NONE);
            }
            else
            {

                patternList1 = new List<(int, byte)>
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
                StageKitLightingController.Instance.SetLed(RED, NONE);
            }

            CuePrimitives.Add(new TimedPattern(patternList1, 1f));

			var patternList2 = new List<(int, byte)> {
                (YELLOW, SIX | TWO),
                (YELLOW, SEVEN | THREE),
                (YELLOW, ZERO | FOUR),
                (YELLOW, ONE | FIVE),
            };
            CuePrimitives.Add(new TimedPattern(patternList2, 2f));
        }
	}
}