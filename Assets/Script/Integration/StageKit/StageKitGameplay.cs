using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using System;
using Random = UnityEngine.Random;

namespace YARG.Integration.StageKit
{
    public class StageKitGameplay : GameplayBehaviour
    {
        public event Action<BeatlineType> HandleBeatline;
        public event Action<int> HandleDrums;
        public event Action<LightingType> HandleLighting;
        public event Action<double> HandleVocals;

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
            _controller = StageKitLightingController.Instance;
            _controller.LargeVenue =
                Random.Range(0, 1) ==
                0; //this should be read from the venue itself eventually but for now, randomize it.
            _venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.FourLaneDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);

            _vocalsIndex = 0;
            _syncIndex = 0;
            _lightingIndex = 0;
            _eventIndex = 0;
            _drumIndex = 0;

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
            if (_vocalsIndex < _vocals.Count - 1 &&
                _vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd <= GameManager.SongTime)
            {
                if (_vocals[_vocalsIndex].PhraseParentNote.Type == VocalNoteType.Lyric)
                {
                    HandleVocals?.Invoke(_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd);
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

            //The lighting cues from the venue track are handled here.
            if (_lightingIndex < _venue.Lighting.Count - 1 &&
                _venue.Lighting[_lightingIndex].Time <= GameManager.SongTime)
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
                    ChangeCues(new ManualWarm());
                    break;

                case LightingType.Cool_Manual:
                    ChangeCues(new ManualCool());
                    break;

                case LightingType.Dischord:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    ChangeCues(new Dischord());
                    break;

                case LightingType.Stomp:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    ChangeCues(new Stomp());
                    break;

                case LightingType.Default:
                    ChangeCues(new Default());
                    break;

                //continuous cues
                case LightingType.Warm_Automatic:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    ChangeCues(new LoopWarm());
                    break;

                case LightingType.Cool_Automatic:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    ChangeCues(new LoopCool());
                    break;

                case LightingType.BigRockEnding:
                    ChangeCues(new BigRockEnding());
                    break;

                case LightingType.Searchlights:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
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

                //instant cues
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
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    break;

                case LightingType.Intro:
                    ChangeCues(new Intro());
                    break;

                //strobe calls
                case LightingType.Strobe_Slow:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Slow);
                    break;

                case LightingType.Strobe_Medium:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Medium);
                    break;

                case LightingType.Strobe_Fast:
                    KillCue(); //This might be a bug in the official code that i'm trying to replicate here, as slow doesn't seem to do it.
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Fast);
                    break;

                case LightingType.Strobe_Fastest:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Fastest);
                    break;

                case LightingType.Strobe_Off:
                    _controller.SetStrobeSpeed(StageKitLightingController.StrobeSpeed.Off);
                    break;

                //Ignored cues
                case LightingType.Keyframe_Next: //these are handled in the cues classes via their primitive calls
                case LightingType.Keyframe_Previous: //no cue listens to this.
                case LightingType.Keyframe_First: //no cue listens to this.
                case LightingType.Menu: // handled in StageKitMenu.cs, shouldn't ever be called here in gameplay.
                case LightingType.Score: // handled in StageKitScore.cs, shouldn't ever be called here in gameplay.

                //In-game lighting calls we currently ignore but might do something with in an extended "funky fresh" mode.
                case LightingType.Verse:
                case LightingType.Chorus:
                    break;

                default:
                    Debug.LogWarning("Unhandled lighting event: " + lightingEvent);
                    break;
            }
        }

        protected override void GameplayDestroy()
        {
            _controller.AllLedsOff();
            KillCue();
            _controller.StageKits.ForEach(kit => kit.ResetHaptics());
        }

        private void ChangeCues(StageKitLightingCue cue)
        {
            KillCue();
            SetupCue(cue);
        }

        private void SetupCue(StageKitLightingCue cue)
        {
            _controller.CurrentLightingCue = cue;
            _controller.CurrentLightingCue.LargeVenue = _controller.LargeVenue;
            _controller.CurrentLightingCue.PreviousLightingCue = _controller.PreviousLightingCue;

            HandleBeatline += _controller.CurrentLightingCue.HandleBeatlineEvent;
            HandleDrums += _controller.CurrentLightingCue.HandleDrumEvent;
            HandleLighting += _controller.CurrentLightingCue.HandleLightingEvent;
            HandleVocals += _controller.CurrentLightingCue.HandleVocalEvent;

            foreach (var primitive in _controller.CurrentLightingCue.CuePrimitives)
            {
                HandleBeatline += primitive.HandleBeatlineEvent;
                HandleDrums += primitive.HandleDrumEvent;
                HandleLighting += primitive.HandleLightingEvent;
                HandleVocals += primitive.HandleVocalEvent;
            }
        }

        private void KillPrimitive(StageKitLighting primitive)
        {
            GameManager.BeatEventManager.Unsubscribe(primitive.OnBeat);

            HandleBeatline -= primitive.HandleBeatlineEvent;
            HandleDrums -= primitive.HandleDrumEvent;
            HandleLighting -= primitive.HandleLightingEvent;
            HandleVocals -= primitive.HandleVocalEvent;

            primitive.CancellationTokenSource?.Cancel();
        }

        private void KillCue()
        {
            if (_controller.CurrentLightingCue == null)
            {
                return;
            }

            HandleBeatline -= _controller.CurrentLightingCue.HandleBeatlineEvent;
            HandleDrums -= _controller.CurrentLightingCue.HandleDrumEvent;
            HandleLighting -= _controller.CurrentLightingCue.HandleLightingEvent;
            HandleVocals -= _controller.CurrentLightingCue.HandleVocalEvent;

            foreach (var primitive in _controller.CurrentLightingCue.CuePrimitives)
            {
                KillPrimitive(primitive);
            }

            _controller.CurrentLightingCue.CuePrimitives.Clear();
            _controller.PreviousLightingCue = _controller.CurrentLightingCue;
            _controller.CurrentLightingCue = null;
        }

        private void OnApplicationQuit()
        {
            _controller.AllLedsOff();
            KillCue();
            _controller.StageKits.ForEach(kit => kit.ResetHaptics());
        }
    }
}