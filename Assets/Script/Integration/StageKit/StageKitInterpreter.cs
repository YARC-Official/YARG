using System;
using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Integration.StageKit
{
    public class StageKitInterpreter : MonoSingleton<StageKitInterpreter>
    {
        private readonly List<StageKitLighting> _cuePrimitives = new();
        private StageKitLightingCue _currentLightingCue;
        public static StageKitLightingCue PreviousLightingCue;
        private const byte NONE = 0b00000000;

        private readonly Dictionary<LightingType, StageKitLightingCue> _cueDictionary = new()
        {
            { LightingType.Menu, new MenuLighting() },
            { LightingType.Score, new ScoreLighting() },
            { LightingType.Warm_Manual, new ManualWarm() },
            { LightingType.Cool_Manual, new ManualCool() },
            { LightingType.Dischord, new Dischord() },
            { LightingType.Stomp, new Stomp() },
            { LightingType.Default, new Default() },
            { LightingType.Warm_Automatic, new LoopWarm() },
            { LightingType.Cool_Automatic, new LoopCool() },
            { LightingType.BigRockEnding, new BigRockEnding() },
            { LightingType.Searchlights, new SearchLight() },
            { LightingType.Frenzy, new Frenzy() },
            { LightingType.Sweep, new Sweep() },
            { LightingType.Harmony, new Harmony() },
            { LightingType.Flare_Slow, new FlareSlow() },
            { LightingType.Flare_Fast, new FlareFast() },
            { LightingType.Silhouettes_Spotlight, new SilhouetteSpot() },
            { LightingType.Silhouettes, new Silhouettes() },
            { LightingType.Blackout_Spotlight, new Blackout() },
            { LightingType.Blackout_Slow, new Blackout() },
            { LightingType.Blackout_Fast, new Blackout() },
            { LightingType.Intro, new Intro() }
        };

        public static event Action<StageKitLedColor, byte> OnLedEvent;

        // This class maintains the Stage Kit lighting cues and primitives
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
            _currentLightingCue = cue;
            _currentLightingCue?.Enable();
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
            if (_currentLightingCue == null) return;

            foreach (var primitive in _currentLightingCue.CuePrimitives)
            {
                primitive.KillSelf();
            }

            _cuePrimitives.Clear();
            PreviousLightingCue = _currentLightingCue;
            _currentLightingCue.DirectListenEnabled = false;
            _currentLightingCue = null;
        }

        protected virtual void OnBeatLineEvent(Beatline value)
        {
            if (_currentLightingCue == null)
            {
                return;
            }

            if (_currentLightingCue.DirectListenEnabled)
            {
                _currentLightingCue.HandleBeatlineEvent(value.Type);
            }

            foreach (var primitive in _currentLightingCue.CuePrimitives)
            {
                primitive.HandleBeatlineEvent(value.Type);
            }
        }

        protected virtual void OnLightingEvent(LightingEvent value)
        {

            if (value != null && value.Type == LightingType.Keyframe_Next && _currentLightingCue != null)
            {
                if (_currentLightingCue.DirectListenEnabled)
                {
                    _currentLightingCue.HandleLightingEvent(value.Type);
                }

                foreach (var primitive in _currentLightingCue.CuePrimitives)
                {
                    primitive.HandleLightingEvent(value.Type);
                }
            }
            else
            {
                if (value == null)
                {
                    ChangeCues(null);
                }
                else if (value.Type is LightingType.Keyframe_Next or LightingType.Keyframe_Previous
                    or LightingType.Keyframe_First or LightingType.Verse or LightingType.Chorus)
                {
                    // Next is handled in the cue classes via their primitive calls.
                    // No cue listens to Previous or First.
                    // Verse and Chorus are ignored by the stage kits but might do something with in an extended "funky fresh" mode.
                }
                else if (_cueDictionary.TryGetValue(value.Type, out var cue))
                {
                    ChangeCues(cue);
                }
                else
                {
                    // Handle cases where the LightingType is not found in the dictionary
                    YargLogger.LogWarning("Unhandled lighting event: " + value?.Type);
                }
            }
        }

        protected virtual void OnDrumEvent(int value)
        {
            if (_currentLightingCue == null)
            {
                return;
            }

            if (_currentLightingCue.DirectListenEnabled)
            {
                _currentLightingCue.HandleDrumEvent(value);
            }

            foreach (var primitive in _currentLightingCue.CuePrimitives)
            {
                primitive.HandleDrumEvent(value);
            }
        }

        protected virtual void OnVocalsEvent(VocalNote value)
        {
            if (_currentLightingCue == null)
            {
                return;
            }

            if (_currentLightingCue.DirectListenEnabled)
            {
                _currentLightingCue.HandleVocalEvent(0);
            }

            foreach (var primitive in _currentLightingCue.CuePrimitives)
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
