using System;
using System.Collections.Generic;
using PlasticBand.Haptics;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Integration;
using Random = UnityEngine.Random;

namespace YARG
{
    public class MasterLightingGameplayMonitor : GameplayBehaviour
    {
        public static VenueTrack _venue;
        public static int _lightingIndex;

        private SyncTrack _sync;
        private List<VocalsPhrase> _vocals;
        private InstrumentDifficulty<DrumNote> _drums;
        private int _eventIndex;
        private int _syncIndex;
        private int _vocalsIndex;
        private int _drumIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            // This should be read from the venue itself eventually, but for now, we'll just randomize it.
            MasterLightingController.LargeVenue = Random.Range(0, 1) == 1;
            _venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.FourLaneDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);

            // Reset the indexes on song restart
            _eventIndex = 0;
            _lightingIndex = 0;
            _syncIndex = 0;
            _vocalsIndex = 0;
            _drumIndex = 0;
        }

        private void Update()
        {
            if (MasterLightingController.Paused != GameManager.Paused)
            {
                MasterLightingController.Paused = GameManager.Paused;
            }

            // Drum events
            while (_drumIndex < _drums.Notes.Count && _drums.Notes[_drumIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentDrumNote = _drums.Notes[_drumIndex];
                _drumIndex++;
            }

            // End of vocal phrase. SilhouetteSpot is the only cue that uses vocals, listening to the end of the phrase.
            while (_vocalsIndex < _vocals.Count &&
                Math.Min(_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd,
                    _vocals[_vocalsIndex].TimeEnd) <= GameManager.SongTime)
            {
                MasterLightingController.CurrentVocalNote = _vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1];
                _vocalsIndex++;
            }

            // Beatline events
            while (_syncIndex < _sync.Beatlines.Count && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentBeatline = _sync.Beatlines[_syncIndex];
                _syncIndex++;
            }

            // The lighting cues from the venue track are handled here.
            while (_lightingIndex < _venue.Lighting.Count &&
                _venue.Lighting[_lightingIndex].Time <= GameManager.SongTime)
            {
                switch (_venue.Lighting[_lightingIndex].Type)
                {
                    case LightingType.Strobe_Off:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Off;
                        break;

                    case LightingType.Strobe_Fast:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Fast;
                        break;

                    case LightingType.Strobe_Medium:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Medium;
                        break;

                    case LightingType.Strobe_Slow:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Slow;
                        break;

                    case LightingType.Strobe_Fastest:
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Fastest;
                        break;

                    default:
                        // Okay so this a bit odd. The stage kit never has the strobe on with a lighting cue.
                        // But the Strobe_Off event is almost never used, relying instead on the cue change to turn it off.
                        // So this technically should be in the stage kit lighting controller code but I don't want the
                        // stage kit reaching into this main lighting controller. Also, Each subclass of the lighting
                        // controller (dmx, stage kit, rgb, etc) could handle this differently but then we have to guess
                        // at how long the strobe should be on. So we'll just turn it off here.
                        MasterLightingController.CurrentStrobeState = StageKitStrobeSpeed.Off;
                        MasterLightingController.CurrentLightingCue = _venue.Lighting[_lightingIndex];
                        break;
                }

                _lightingIndex++;
            }

            // For "fogOn", "fogOff", and "BonusFx" events
            while (_eventIndex < _venue.Stage.Count && _venue.Stage[_eventIndex].Time <= GameManager.SongTime)
            {
                if (_venue.Stage[_eventIndex].Effect == StageEffect.FogOn)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.On;
                }
                else if (_venue.Stage[_eventIndex].Effect == StageEffect.FogOff)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.Off;
                }
                else if (_venue.Stage[_eventIndex].Effect == StageEffect.BonusFx)
                {
                    MasterLightingController.FireBonusFXEvent();
                }

                _eventIndex++;
            }
        }
    }
}
/*
    "I hope that after I die, people will say of me: 'That guy sure owed me a lot of money.'"

    - Jack Handey.
*/