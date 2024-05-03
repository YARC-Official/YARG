using System;
using System.Collections.Generic;
using PlasticBand.Haptics;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using Random = UnityEngine.Random;

namespace YARG.Integration
{
    public class MasterLightingGameplayMonitor : GameplayBehaviour
    {
        private enum FiveFretsEnum
        {
            Off = 0,
            Red = 1,
            Yellow = 2,
            Blue = 4,
            Green = 8,
            Orange = 16,
            //Open isn't used by Keys
            Open = 32,
        }

        private enum DrumEnum
        {
            Off = 0,
            RedDrum = 1,
            YellowDrum = 2,
            BlueDrum = 4,
            GreenDrum = 8,
            Kick = 16,
            YellowCymbal = 32,
            BlueCymbal = 64,
            GreenCymbal = 128,
        }

        public static VenueTrack Venue { get; private set; }
        public static int LightingIndex { get; private set; }

        private SyncTrack _sync;
        private List<VocalsPhrase> _vocals;
        private InstrumentDifficulty<DrumNote> _drums;
        private InstrumentDifficulty<GuitarNote> _guitar;
        private InstrumentDifficulty<GuitarNote> _bass;
        private InstrumentDifficulty<GuitarNote> _keys;

        private int _keysIndex;
        private int _syncIndex;
        private int _vocalsIndex;
        private int _drumIndex;
        private int _guitarIndex;
        private int _bassIndex;
        private int _stageIndex;
        //NYI
        //private int _performerIndex;
        private int _postProcessingIndex;

        private int _guitarEndCheckIndex = -1;
        private int _bassEndCheckIndex = -1;
        private int _drumEndCheckIndex = -1;
        private int _keysEndCheckIndex = -1;

        protected override void OnChartLoaded(SongChart chart)
        {
            // This should be read from the venue itself eventually, but for now, we'll just randomize it.
            MasterLightingController.LargeVenue = Random.Range(0, 1) == 1;
            Venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.ProDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);
            chart.FiveFretGuitar.Difficulties.TryGetValue(Difficulty.Expert, out _guitar);
            chart.FiveFretBass.Difficulties.TryGetValue(Difficulty.Expert, out _bass);
            chart.Keys.Difficulties.TryGetValue(Difficulty.Expert, out _keys);

            // Reset the indexes on song restart
            _stageIndex = 0;
            LightingIndex = 0;
            _syncIndex = 0;
            _vocalsIndex = 0;
            _guitarIndex = 0;
            _bassIndex = 0;
            _drumIndex = 0;
            //NYI
            //_performerIndex = 0;
            _postProcessingIndex = 0;
            _keysIndex = 0;

        }

        private void Update()
        {

            if (MasterLightingController.Paused != GameManager.Paused)
            {
                MasterLightingController.Paused = GameManager.Paused;
            }

            // Keys events
            while (_keysIndex < _keys.Notes.Count && _keys.Notes[_keysIndex].Time <= GameManager.SongTime)
            {
                int fretsPressed = (int)FiveFretsEnum.Off;

                foreach (var c in _keys.Notes[_keysIndex].ChildNotes)
                {
                    switch (c.Fret)
                    {
                        case (int)FiveFretGuitarFret.Red:
                            fretsPressed += (int)FiveFretsEnum.Red;
                            break;

                        case (int)FiveFretGuitarFret.Yellow:
                            fretsPressed += (int)FiveFretsEnum.Yellow;
                            break;

                        case (int)FiveFretGuitarFret.Blue:
                            fretsPressed += (int)FiveFretsEnum.Blue;
                            break;

                        case (int)FiveFretGuitarFret.Green:
                            fretsPressed += (int)FiveFretsEnum.Green;
                            break;

                        case (int)FiveFretGuitarFret.Orange:
                            fretsPressed += (int)FiveFretsEnum.Orange;
                            break;

                    }
                }

                switch (_keys.Notes[_keysIndex].Fret)
                {
                    case (int)FiveFretGuitarFret.Red:
                        fretsPressed += (int)FiveFretsEnum.Red;
                        break;

                    case (int)FiveFretGuitarFret.Yellow:
                        fretsPressed += (int)FiveFretsEnum.Yellow;
                        break;

                    case (int)FiveFretGuitarFret.Blue:
                        fretsPressed += (int)FiveFretsEnum.Blue;
                        break;

                    case (int)FiveFretGuitarFret.Green:
                        fretsPressed += (int)FiveFretsEnum.Green;
                        break;

                    case (int)FiveFretGuitarFret.Orange:
                        fretsPressed += (int)FiveFretsEnum.Orange;
                        break;

                }
                _keysEndCheckIndex = _keysIndex;
                MasterLightingController.CurrentKeysNotes = fretsPressed;
                _keysIndex++;
            }
            if (_keysEndCheckIndex != -1 && _keys.Notes[_keysEndCheckIndex].TimeEnd <= GameManager.SongTime)
            {
                MasterLightingController.CurrentKeysNotes = (int)FiveFretsEnum.Off;
                _keysEndCheckIndex  = -1;
            }
            //----


            // Bass events
            while (_bassIndex < _bass.Notes.Count && _bass.Notes[_bassIndex].Time <= GameManager.SongTime)
            {
                int fretsPressed =  (int)FiveFretsEnum.Off;

                foreach (var c in _bass.Notes[_bassIndex].ChildNotes)
                {
                    switch (c.Fret)
                    {
                        case (int)FiveFretGuitarFret.Red:
                            fretsPressed += (int)FiveFretsEnum.Red;
                            break;

                        case (int)FiveFretGuitarFret.Yellow:
                            fretsPressed += (int)FiveFretsEnum.Yellow;
                            break;

                        case (int)FiveFretGuitarFret.Blue:
                            fretsPressed += (int)FiveFretsEnum.Blue;
                            break;

                        case (int)FiveFretGuitarFret.Green:
                            fretsPressed += (int)FiveFretsEnum.Green;
                            break;

                        case (int)FiveFretGuitarFret.Orange:
                            fretsPressed += (int)FiveFretsEnum.Orange;
                            break;

                        case (int)FiveFretGuitarFret.Open:
                            fretsPressed += (int)FiveFretsEnum.Open;
                            break;

                    }
                }

                switch (_bass.Notes[_bassIndex].Fret)
                {
                    case (int)FiveFretGuitarFret.Red:
                        fretsPressed += (int)FiveFretsEnum.Red;
                        break;

                    case (int)FiveFretGuitarFret.Yellow:
                        fretsPressed += (int)FiveFretsEnum.Yellow;
                        break;

                    case (int)FiveFretGuitarFret.Blue:
                        fretsPressed += (int)FiveFretsEnum.Blue;
                        break;

                    case (int)FiveFretGuitarFret.Green:
                        fretsPressed += (int)FiveFretsEnum.Green;
                        break;

                    case (int)FiveFretGuitarFret.Orange:
                        fretsPressed += (int)FiveFretsEnum.Orange;
                        break;

                    case (int)FiveFretGuitarFret.Open:
                        fretsPressed += (int)FiveFretsEnum.Open;
                        break;

                }
                _bassEndCheckIndex = _bassIndex;
                MasterLightingController.CurrentBassNotes = fretsPressed;
                _bassIndex++;
            }
            if (_bassEndCheckIndex != -1 && _bass.Notes[_bassEndCheckIndex].TimeEnd <= GameManager.SongTime)
            {
                MasterLightingController.CurrentBassNotes = (int)FiveFretsEnum.Off;
                _bassEndCheckIndex  = -1;
            }
            //----

            // Guitar events
            while (_guitarIndex < _guitar.Notes.Count && _guitar.Notes[_guitarIndex].Time <= GameManager.SongTime)
            {
                int fretsPressed =  (int)FiveFretsEnum.Off;

                foreach (var c in _guitar.Notes[_guitarIndex].ChildNotes)
                {
                    switch (c.Fret)
                    {
                        case (int)FiveFretGuitarFret.Red:
                            fretsPressed += (int)FiveFretsEnum.Red;
                            break;

                        case (int)FiveFretGuitarFret.Yellow:
                            fretsPressed += (int)FiveFretsEnum.Yellow;
                            break;

                        case (int)FiveFretGuitarFret.Blue:
                            fretsPressed += (int)FiveFretsEnum.Blue;
                            break;

                        case (int)FiveFretGuitarFret.Green:
                            fretsPressed += (int)FiveFretsEnum.Green;
                            break;

                        case (int)FiveFretGuitarFret.Orange:
                            fretsPressed += (int)FiveFretsEnum.Orange;
                            break;

                        case (int)FiveFretGuitarFret.Open:
                            fretsPressed += (int)FiveFretsEnum.Open;
                            break;

                    }
                }

                switch (_guitar.Notes[_guitarIndex].Fret)
                {
                    case (int)FiveFretGuitarFret.Red:
                        fretsPressed += (int)FiveFretsEnum.Red;
                        break;

                    case (int)FiveFretGuitarFret.Yellow:
                        fretsPressed += (int)FiveFretsEnum.Yellow;
                        break;

                    case (int)FiveFretGuitarFret.Blue:
                        fretsPressed += (int)FiveFretsEnum.Blue;
                        break;

                    case (int)FiveFretGuitarFret.Green:
                        fretsPressed += (int)FiveFretsEnum.Green;
                        break;

                    case (int)FiveFretGuitarFret.Orange:
                        fretsPressed += (int)FiveFretsEnum.Orange;
                        break;

                    case (int)FiveFretGuitarFret.Open:
                        fretsPressed += (int)FiveFretsEnum.Open;
                        break;

                }

                MasterLightingController.CurrentGuitarNotes = fretsPressed;
                _guitarEndCheckIndex = _guitarIndex;
                _guitarIndex++;
            }

            if (_guitarEndCheckIndex != -1 && _guitar.Notes[_guitarEndCheckIndex].TimeEnd <= GameManager.SongTime)
            {
                MasterLightingController.CurrentGuitarNotes = (int)FiveFretsEnum.Off;
                _guitarEndCheckIndex  = -1;
            }
            //----

            // Drum events
            while (_drumIndex < _drums.Notes.Count && _drums.Notes[_drumIndex].Time <= GameManager.SongTime)
            {
                int padsHit = (int)DrumEnum.Off;

                foreach (var c in _drums.Notes[_drumIndex].ChildNotes)
                {
                    switch (c.Pad)
                    {
                        case (int)FourLaneDrumPad.RedDrum:
                            padsHit += (int)DrumEnum.RedDrum;
                            break;

                        case (int)FourLaneDrumPad.YellowDrum:
                            padsHit += (int)DrumEnum.YellowDrum;
                            break;

                        case (int)FourLaneDrumPad.BlueDrum:
                            padsHit += (int)DrumEnum.BlueDrum;
                            break;

                        case (int)FourLaneDrumPad.GreenDrum:
                            padsHit += (int)DrumEnum.GreenDrum;
                            break;

                        case (int)FourLaneDrumPad.Kick:
                            padsHit += (int)DrumEnum.Kick;
                            break;

                        case (int)FourLaneDrumPad.YellowCymbal:
                            padsHit += (int)DrumEnum.YellowCymbal;
                            break;

                        case (int)FourLaneDrumPad.BlueCymbal:
                            padsHit += (int)DrumEnum.BlueCymbal;
                            break;

                        case (int)FourLaneDrumPad.GreenCymbal:
                            padsHit += (int)DrumEnum.GreenCymbal;
                            break;
                    }
                }

                switch (_drums.Notes[_drumIndex].Pad)
                {
                    case (int)FourLaneDrumPad.RedDrum:
                        padsHit += (int)DrumEnum.RedDrum;
                        break;

                    case (int)FourLaneDrumPad.YellowDrum:
                        padsHit += (int)DrumEnum.YellowDrum;
                        break;

                    case (int)FourLaneDrumPad.BlueDrum:
                        padsHit += (int)DrumEnum.BlueDrum;
                        break;

                    case (int)FourLaneDrumPad.GreenDrum:
                        padsHit += (int)DrumEnum.GreenDrum;
                        break;

                    case (int)FourLaneDrumPad.Kick:
                        padsHit += (int)DrumEnum.Kick;
                        break;

                    case (int)FourLaneDrumPad.YellowCymbal:
                        padsHit += (int)DrumEnum.YellowCymbal;
                        break;

                    case (int)FourLaneDrumPad.BlueCymbal:
                        padsHit += (int)DrumEnum.BlueCymbal;
                        break;

                    case (int)FourLaneDrumPad.GreenCymbal:
                        padsHit += (int)DrumEnum.GreenCymbal;
                        break;
                }

                MasterLightingController.CurrentDrumNotes = padsHit;
                _drumEndCheckIndex = _drumIndex;
                _drumIndex++;
            }

            if (_drumEndCheckIndex != -1 && _drums.Notes[_drumEndCheckIndex].TimeEnd <= GameManager.SongTime)
            {
                MasterLightingController.CurrentDrumNotes = (int)DrumEnum.Off;
                _drumEndCheckIndex  = -1;
            }
            //----

            // End of vocal phrase. SilhouetteSpot is the only cue that uses vocals, listening to the end of the phrase.
            while (_vocalsIndex < _vocals.Count && Math.Min(_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd, _vocals[_vocalsIndex].TimeEnd) <= GameManager.SongTime)
            {
                MasterLightingController.CurrentVocalNote = _vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1];
                _vocalsIndex++;
            }

            //Camera Cut events
            // NYI - waiting for parser rewrite

            // Performer events
            // NYI - waiting for parser rewrite
            //while (_performerIndex < Venue.Performer.Count && Venue.Performer[_performerIndex].Time <= GameManager.SongTime)
            //{
                //performerEventEndtime = Venue.Performer[0].TimeEnd;
                //MasterLightingController.CurrentPerformerEvent = Venue.Performer[_performerIndex];
                //_performerIndex++;
            //}

            // Post processing events
            while (_postProcessingIndex < Venue.PostProcessing.Count && Venue.PostProcessing[_postProcessingIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentPostProcessing = Venue.PostProcessing[_postProcessingIndex];
                _postProcessingIndex++;
            }

            // Beatline events
            while (_syncIndex < _sync.Beatlines.Count && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.CurrentBeatline = _sync.Beatlines[_syncIndex];
                _syncIndex++;
            }

            // The lighting cues from the venue track are handled here.
            while (LightingIndex < Venue.Lighting.Count && Venue.Lighting[LightingIndex].Time <= GameManager.SongTime)
            {
                switch (Venue.Lighting[LightingIndex].Type)
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
                        MasterLightingController.CurrentLightingCue = Venue.Lighting[LightingIndex];
                        break;
                }

                LightingIndex++;
            }

            // For "fogOn", "fogOff", and "BonusFx" events
            while (_stageIndex < Venue.Stage.Count && Venue.Stage[_stageIndex].Time <= GameManager.SongTime)
            {
                if (Venue.Stage[_stageIndex].Effect == StageEffect.FogOn)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.On;
                }
                else if (Venue.Stage[_stageIndex].Effect == StageEffect.FogOff)
                {
                    MasterLightingController.CurrentFogState = MasterLightingController.FogState.Off;
                }
                else if (Venue.Stage[_stageIndex].Effect == StageEffect.BonusFx)
                {
                    MasterLightingController.FireBonusFXEvent();
                }

                _stageIndex++;
            }
        }
    }
}

/*
    "I hope that after I die, people will say of me: 'That guy sure owed me a lot of money.'"

    - Jack Handey.
*/
