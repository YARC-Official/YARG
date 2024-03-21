using System;
using System.Collections;
using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Integration;
using YARG.Integration.StageKit;

namespace YARG
{
    public class StageKitInterpreter : MonoSingleton<StageKitInterpreter>
    {

        private readonly List<StageKitLighting> _cuePrimitives = new();
        public StageKitLightingCue CurrentLightingCue;
        public StageKitLightingCue PreviousLightingCue;
        private const byte NONE = 0b00000000;
        private GameManager GameManager { get; set; }

        public static event Action<StageKitLedColor, byte> OnLedEvent;

        // this class maintains the Stage Kit lighting cues and primitives
        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
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

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == (int) SceneIndex.Gameplay)
            {
                GameManager = FindObjectOfType<GameManager>();
            }
        }

        private void ChangeCues(StageKitLightingCue cue)
        {
            KillCue();
            CurrentLightingCue = cue;
        }

        public void SetLed(StageKitLedColor color, byte led)
        {
            OnLedEvent?.Invoke(color, led);
            //_currentLedState[color] = led;
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
            if (MasterLightingController.CurrentLightingCue == null) return;

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                if ((int) SceneIndex.Gameplay == SceneManager.GetActiveScene().buildIndex)
                {
                    //When a BeatPattern primitive is created, it subscribes its OnBeat to the BeatEventManager.
                    GameManager.BeatEventHandler.Unsubscribe(primitive.OnBeat);
                }

                primitive.CancellationTokenSource?.Cancel();
            }

            _cuePrimitives.Clear();
            PreviousLightingCue = CurrentLightingCue;
            CurrentLightingCue = null;
        }

        protected virtual void OnBeatLineEvent(Beatline value)
        {
            if (MasterLightingController.CurrentLightingCue == null)
            {
                return;
            }

            CurrentLightingCue.HandleBeatlineEvent(value.Type);

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.HandleBeatlineEvent(value.Type);
            }
        }

        protected virtual void OnLightingEvent(LightingEvent value)
        {
            if (value.Type == LightingType.Keyframe_Next && MasterLightingController.CurrentLightingCue != null)
            {
                CurrentLightingCue.HandleLightingEvent(value.Type);

                foreach (var primitive in CurrentLightingCue.CuePrimitives)
                {
                    primitive.HandleLightingEvent(value.Type);
                }
            }
            else
            {
                switch (value.Type)
                {
                    case LightingType.Menu:
                        ChangeCues(new MenuLighting());
                        break;

                    case LightingType.Score:
                        ChangeCues(new ScoreLighting());
                        break;

                    //Key Framed cues
                    case LightingType.Warm_Manual:
                        ChangeCues(new ManualWarm());
                        break;

                    case LightingType.Cool_Manual:
                        ChangeCues(new ManualCool());
                        break;

                    case LightingType.Dischord:
                        ChangeCues(new Dischord());
                        break;

                    case LightingType.Stomp:
                        ChangeCues(new Stomp());
                        break;

                    case LightingType.Default:
                        ChangeCues(new Default());
                        break;

                    //Continuous cues
                    case LightingType.Warm_Automatic:
                        ChangeCues(new LoopWarm());
                        break;

                    case LightingType.Cool_Automatic:
                        ChangeCues(new LoopCool());
                        break;

                    case LightingType.BigRockEnding:
                        ChangeCues(new BigRockEnding());
                        break;

                    case LightingType.Searchlights:
                        ChangeCues(new SearchLight());
                        break;

                    case LightingType.Frenzy:
                        ChangeCues(new Frenzy());
                        break;

                    case LightingType.Sweep:
                        ChangeCues(new Sweep());
                        break;

                    case LightingType.Harmony:
                        ChangeCues(new Harmony());
                        break;

                    //Instant cues
                    case LightingType.Flare_Slow:
                        ChangeCues(new FlareSlow());
                        break;

                    case LightingType.Flare_Fast:
                        ChangeCues(new FlareFast());
                        break;

                    case LightingType.Silhouettes_Spotlight:
                        ChangeCues(new SilhouetteSpot());
                        break;

                    case LightingType.Silhouettes:
                        ChangeCues(new Silhouettes());
                        break;

                    case LightingType.Blackout_Spotlight:
                    case LightingType.Blackout_Slow:
                    case LightingType.Blackout_Fast:
                        ChangeCues(new Blackout());
                        break;

                    case LightingType.Intro:
                        ChangeCues(new Intro());
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
                        Debug.LogWarning("(Lighting Integration Parent) Unhandled lighting event: " + value.Type);
                        break;
                }
            }
        }

        protected virtual void OnDrumEvent(DrumNote value)
        {
            if (MasterLightingController.CurrentLightingCue == null)
            {
                return;
            }

            CurrentLightingCue.HandleDrumEvent(value.Pad);

            foreach (var primitive in CurrentLightingCue.CuePrimitives)
            {
                primitive.HandleDrumEvent(value.Pad);
            }
        }

        protected virtual void OnVocalsEvent(VocalNote value)
        {
            if (MasterLightingController.CurrentLightingCue == null)
            {
                return;
            }

            CurrentLightingCue.HandleVocalEvent(0);

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