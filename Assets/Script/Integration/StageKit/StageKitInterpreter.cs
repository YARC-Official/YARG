using System;
using System.Collections.Generic;
using System.Linq;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Integration;
using YARG.Integration.StageKit;

namespace YARG
{
    public class StageKitInterpreter : MonoSingleton<StageKitInterpreter>
    {
        private readonly List<StageKitLighting> _cuePrimitives = new();
        public StageKitLightingCue CurrentLightingCue;
        public static StageKitLightingCue PreviousLightingCue;
        private const byte NONE = 0b00000000;

        private readonly List<StageKitLightingCue> _cuesList = new List<StageKitLightingCue>
        {
            new MenuLighting(),
            new ScoreLighting(),
            new ManualWarm(),
            new ManualCool(),
            new Dischord(),
            new Stomp(),
            new Default(),
            new LoopWarm(),
            new LoopCool(),
            new BigRockEnding(),
            new SearchLight(),
            new Frenzy(),
            new Sweep(),
            new Harmony(),
            new FlareSlow(),
            new FlareFast(),
            new SilhouetteSpot(),
            new Silhouettes(),
            new Blackout(),
            new Intro()
        };

        public static event Action<StageKitLedColor, byte> OnLedEvent;

        // this class maintains the Stage Kit lighting cues and primitives
        public void Start()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            MasterLightingController.OnDrumEvent += OnDrumEvent;
            MasterLightingController.OnVocalsEvent += OnVocalsEvent;
            MasterLightingController.OnLightingEvent += OnLightingEvent;
            MasterLightingController.OnBeatLineEvent += OnBeatLineEvent;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            AllLedsOff();
            KillCue();
        }

        private void ChangeCues(StageKitLightingCue cue)
        {
            KillCue();
            CurrentLightingCue = cue;
            CurrentLightingCue?.Enable();
        }

        public void SetLed(StageKitLedColor color, byte led)
        {
            OnLedEvent?.Invoke(color, led);
        }

        private void AllLedsOff()
        {
            SetLed(StageKitLedColor.Red, NONE);
            SetLed(StageKitLedColor.Green, NONE);
            SetLed(StageKitLedColor.Blue, NONE);
            SetLed(StageKitLedColor.Yellow, NONE);
        }

        private void KillCue()
        {
            if (CurrentLightingCue == null) return;

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.KillSelf();
            }

            _cuePrimitives.Clear();
            PreviousLightingCue = CurrentLightingCue;
            CurrentLightingCue.DirectListenEnabled = false;
            CurrentLightingCue = null;
        }

        protected virtual void OnBeatLineEvent(Beatline value)
        {
            if (CurrentLightingCue == null)
            {
                return;
            }

            if (CurrentLightingCue.DirectListenEnabled)
            {
                CurrentLightingCue.HandleBeatlineEvent(value.Type);
            }

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.HandleBeatlineEvent(value.Type);
            }
        }

        protected virtual void OnLightingEvent(LightingEvent value)
        {
            if (value != null && value.Type == LightingType.Keyframe_Next)
            {
                if (CurrentLightingCue.DirectListenEnabled)
                {
                    CurrentLightingCue.HandleLightingEvent(value.Type);
                }

                foreach (var primitive in CurrentLightingCue.CuePrimitives)
                {
                    primitive.HandleLightingEvent(value.Type);
                }
            }
            else
            {
                switch (value?.Type)
                {
                    case null:
                        ChangeCues(null);
                        break;

                    case LightingType.Menu:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is MenuLighting));
                        break;

                    case LightingType.Score:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is ScoreLighting));
                        break;

                    //Key Framed cues
                    case LightingType.Warm_Manual:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is ManualWarm));
                        break;

                    case LightingType.Cool_Manual:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is ManualCool));
                        break;

                    case LightingType.Dischord:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Dischord));
                        break;

                    case LightingType.Stomp:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Stomp));
                        break;

                    case LightingType.Default:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Default));
                        break;

                    //Continuous cues
                    case LightingType.Warm_Automatic:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is LoopWarm));
                        break;

                    case LightingType.Cool_Automatic:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is LoopCool));
                        break;

                    case LightingType.BigRockEnding:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is BigRockEnding));
                        break;

                    case LightingType.Searchlights:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is SearchLight));
                        break;

                    case LightingType.Frenzy:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Frenzy));
                        break;

                    case LightingType.Sweep:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Sweep));
                        break;

                    case LightingType.Harmony:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Harmony));
                        break;

                    //Instant cues
                    case LightingType.Flare_Slow:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is FlareSlow));
                        break;

                    case LightingType.Flare_Fast:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is FlareFast));
                        break;

                    case LightingType.Silhouettes_Spotlight:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is SilhouetteSpot));
                        break;

                    case LightingType.Silhouettes:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Silhouettes));
                        break;

                    case LightingType.Blackout_Spotlight:
                    case LightingType.Blackout_Slow:
                    case LightingType.Blackout_Fast:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Blackout));
                        break;

                    case LightingType.Intro:
                        ChangeCues(_cuesList.FirstOrDefault(c => c is Intro));
                        break;

                    //Ignored cues
                    //these are handled in the cue classes via their primitive calls.
                    case LightingType.Keyframe_Next:
                    //no cue listens to Previous.
                    case LightingType.Keyframe_Previous:
                    //no cue listens to First.
                    case LightingType.Keyframe_First:

                    //In-game lighting calls we currently ignore but might do something with in an
                    //extended "funky fresh" mode.
                    case LightingType.Verse:
                    case LightingType.Chorus:
                        break;

                    default:
                        YargLogger.LogWarning("Unhandled lighting event: " + value.Type);
                        break;
                }
            }
        }

        protected virtual void OnDrumEvent(DrumNote value)
        {
            if (CurrentLightingCue == null)
            {
                return;
            }

            if (CurrentLightingCue.DirectListenEnabled)
            {
                CurrentLightingCue.HandleDrumEvent(value.Pad);
            }

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.HandleDrumEvent(value.Pad);
            }
        }

        protected virtual void OnVocalsEvent(VocalNote value)
        {
            if (CurrentLightingCue == null)
            {
                return;
            }

            if (CurrentLightingCue.DirectListenEnabled)
            {
                CurrentLightingCue.HandleVocalEvent(0);
            }

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.HandleVocalEvent(0);
            }
        }
    }
}
/*
    "It takes a big man to cry, but it takes an even bigger man to laugh at that man."

        - Jack Handey
*/