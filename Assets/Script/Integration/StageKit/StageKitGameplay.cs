using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using System;
using Random = UnityEngine.Random;

namespace YARG
{
    public class StageKitGameplay : GameplayBehaviour
    {
        public bool LargeVenue = Random.Range(0,1) == 0; //this should be read from the venue itself but for now, randomize it.
        public GameManager GameManger;
        public event Action<BeatlineType> HandleBeatline;
        public event Action<int> HandleDrums;
        public event Action<LightingType> HandleLighting;
        public event Action<double> HandleVocals;
        public static StageKitGameplay Instance { get; private set; }

        private VenueTrack _venue;
        private SyncTrack _sync;
        private List<VocalsPhrase> _vocals;
        private InstrumentDifficulty<DrumNote> _drums;
        private bool _onPause = false;
        private int _eventIndex;
        private int _lightingIndex;
        private int _syncIndex;
        private int _vocalsIndex;
        private int _drumIndex;
        private StageKitLightingController _controller;

        protected override void OnChartLoaded(SongChart chart)
        {
            GameManger = GameManager;
            Instance = this;
            _controller = StageKitLightingController.Instance;
            _venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.FourLaneDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);

            _vocalsIndex = 0;
            _syncIndex = 0;
            _lightingIndex = 0;
            _eventIndex = 0;
            _drumIndex = 0;

            _controller.CurrentLightingCue?.Dispose(true);
            _controller.CurrentLightingCue = null;
            StageKitLightingController.Instance.StageKits.ForEach(kit => kit.ResetHaptics());

        }

       private void Update()
        {
            //On Pause, turn off the fog and strobe so people don't die, but leave the leds on, looks nice.
            //Is there a OnPause/OnResume event? that would simplify this.
            if (GameManager.Paused && _onPause == false)
            {
                _controller.PreviousFogState = _controller.CurrentFogState;
                _controller.PreviousStrobeState = _controller.CurrentStrobeState;
                _controller.SetFogMachine(StageKitLightingController.FogState.Off);
                _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                _onPause = true;
            }
            else if (_onPause)
            {
                _controller.SetFogMachine(_controller.PreviousFogState);
                _controller.SetStrobeSpeed(_controller.PreviousStrobeState);
                _onPause = false;
            }

            //how we get the current event for each track
            // Dischord listens to the red pad
            if (_drumIndex < _drums.Notes.Count - 1 && _drums.Notes[_drumIndex].Time <= GameManager.SongTime)
            {
                HandleDrums?.Invoke(_drums.Notes[_drumIndex].Pad);
                _drumIndex++;
            }

            //SilhouetteSpot is the only cue that uses vocals, listening to the end of the phrase.
             if (_vocalsIndex < _vocals.Count - 1 && _vocals[_vocalsIndex].Notes[^1].TotalTimeEnd <= GameManager.SongTime)
             {
                 if (_vocals[_vocalsIndex].Type == VocalsPhraseType.Lyric)
                 {
                     HandleVocals?.Invoke(_vocals[_vocalsIndex].Notes[^1].TotalTimeEnd);
                 }
                 _vocalsIndex++;
             }

            //"Major" and "Minor" are now "Measure" and "Strong", respectively. I've never encountered "Weak" in any official chart and don't know what that used to be called, if anything.
            // Any beat timed cue primitive listens to these.
            if (_syncIndex < _sync.Beatlines.Count - 1 && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                HandleBeatline?.Invoke(_sync.Beatlines[_syncIndex].Type);
                _syncIndex++;
            }

            //Lighting calls for the stage kits
            //triggers the actual cues.
            if (_lightingIndex < _venue.Lighting.Count - 1 && _venue.Lighting[_lightingIndex].Time <= GameManager.SongTime)
            {

                if (_venue.Lighting[_lightingIndex].Type == LightingType.Keyframe_Next)
                {
                    HandleLighting?.Invoke(_venue.Lighting[_lightingIndex].Type);
                }
                else
                {
                    HandleVenue(_venue.Lighting[_lightingIndex].Type);
                }
                _lightingIndex++;
            }

            //For "fogOn", "fogOff", and "BonusFx" events
            if (_eventIndex >= _venue.Stage.Count - 1 || !(_venue.Stage[_eventIndex].Time <= GameManager.SongTime))
            {
                return;
            }

            switch (_venue.Stage[_eventIndex].Effect)
            {
                case StageEffect.FogOn:
                    _controller.SetFogMachine(StageKitLightingController.FogState.On);
                    break;

                case StageEffect.FogOff:
                    _controller.SetFogMachine(StageKitLightingController.FogState.Off);
                    break;

                case StageEffect.BonusFx:
                    //Currently ignored but might do something with in an extended "funky fresh" mode.
                    break;

                default:
                    Debug.LogWarning("Unknown stage effect: " + _venue.Stage[_eventIndex].Effect);
                    break;
            }
            _eventIndex++;
        }

         private void HandleVenue(LightingType lightingEvent)
        {
		    switch (lightingEvent)
            {
                //keyframed cues
                case LightingType.Warm_Manual:
                    _controller.CurrentLightingCue?.Dispose();// there was some damn reason to have this here instead of the Start() of the cue, but I can't remember why.
                    _controller.CurrentLightingCue = new ManualWarm();
                    break;

			    case LightingType.Cool_Manual:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new ManualCool();
				    break;

			    case LightingType.Dischord:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Dischord();
				    break;

			    case LightingType.Stomp:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Stomp();
				    break;

			    case LightingType.Default:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Default();
				    break;

                //continuous cues
                case LightingType.Warm_Automatic:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue = new LoopWarm();
                    break;

                case LightingType.Cool_Automatic:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue = new LoopCool();
                    break;

                case LightingType.BigRockEnding:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new BigRockEnding();
                    break;

			    case LightingType.Searchlights:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue = new SearchLight();
				    break;

			    case LightingType.Frenzy:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Frenzy();
				    break;

                case LightingType.Sweep:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Sweep();
                    break;

                case LightingType.Harmony:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Harmony();
                    break;

                //instant cues
                case LightingType.Flare_Slow:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new FlareSlow();
                    break;

			    case LightingType.Flare_Fast:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new FlareFast();
				    break;

			    case LightingType.Silhouettes_Spotlight:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new SilhouetteSpot();
				    break;

			    case LightingType.Silhouettes:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Silhouettes();
				    break;

                case LightingType.Blackout_Spotlight:
                case LightingType.Blackout_Slow:
			    case LightingType.Blackout_Fast:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    _controller.CurrentLightingCue = new Blackout();
				    break;

                case LightingType.Intro:
                    _controller.CurrentLightingCue?.Dispose();
                    _controller.CurrentLightingCue = new Intro();
                    break;

			    //strobe calls
                //Medium, Fastest, and Off are not used in ANY official chart. However, the stagekit DOES actually support them so a charter in the future could use them.
			    case LightingType.Strobe_Slow:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Slow);
				    break;

			    case LightingType.Strobe_Medium:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Medium);
				    break;

			    case LightingType.Strobe_Fast:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Fast);
				    break;

			    case LightingType.Strobe_Fastest:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Fastest);
				    break;

			    case LightingType.Strobe_Off:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
				    break;

                //Ignored cues
                case LightingType.Keyframe_Next: //these are handled in the cues via their primitive calls
                case LightingType.Keyframe_Previous: //no cue listens to this.
                case LightingType.Keyframe_First: //no cue listens to this.
                case LightingType.Menu: // handled in StageKitMenu.cs, shouldn't ever be called here.
                case LightingType.Score: // handled in StageKitScore.cs, shouldn't ever be called here.

                //In-game lighting calls we currently ignore but might do something with in an extended "funky fresh" mode.
                case LightingType.Verse:
                case LightingType.Chorus:
                    break;

                default:
				    Debug.Log("Unhandled lighting event: " + lightingEvent);
				    break;
		    }
	    }

         protected override void GameplayDestroy()
         {
             _controller.CurrentLightingCue?.Dispose(true);
             _controller.CurrentLightingCue = null;
             _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
             _controller.SetFogMachine(StageKitLightingController.FogState.Off);
         }
    }
}