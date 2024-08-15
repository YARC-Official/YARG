using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using Random = UnityEngine.Random;

namespace YARG.Integration
{
    public class MasterLightingGameplayMonitor : GameplayBehaviour
    {
        private struct VocalNoteEvent
        {
            public float Pitch;
            public double StartTime;
            public double EndTime;
            public bool IsActive;
        }

        public static VenueTrack Venue { get; set; }
        public static int LightingIndex { get; set; }

        private SyncTrack _sync;

        private List<VocalsPhrase> _vocals;
        private List<VocalsPhrase> _harmony0;
        private List<VocalsPhrase> _harmony1;
        private List<VocalsPhrase> _harmony2;
        private InstrumentDifficulty<DrumNote> _drums;
        private InstrumentDifficulty<GuitarNote> _guitar;
        private InstrumentDifficulty<GuitarNote> _bass;
        private InstrumentDifficulty<GuitarNote> _keys;

        private int _vocalsIndex;
        private int _harmony0Index;
        private int _harmony1Index;
        private int _harmony2Index;
        private int _keysIndex;
        private int _syncIndex;
        private int _bpmIndex;
        private int _drumIndex;
        private int _guitarIndex;
        private int _bassIndex;
        private int _stageIndex;
        //NYI
        //private int _performerIndex;
        private int _postProcessingIndex;

        private List<VocalNoteEvent> _vocalsNotes;
        private List<VocalNoteEvent> _harmony0Notes;
        private List<VocalNoteEvent> _harmony1Notes;
        private List<VocalNoteEvent> _harmony2Notes;

        protected override void OnChartLoaded(SongChart chart)
        {
            MasterLightingController.MLCFogState = false;
            MasterLightingController.MLCStrobeState = (byte) StageKitStrobeSpeed.Off;
            MasterLightingController.Initializer(SceneManager.GetActiveScene());

            // This should be read from the venue itself eventually, but for now, we'll just randomize it.
            MasterLightingController.MLCLargeVenue = Random.Range(0, 1) == 1;
            Venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            _harmony0 = chart.Harmony.Parts[0].NotePhrases;
            _harmony1 = chart.Harmony.Parts[1].NotePhrases;
            _harmony2 = chart.Harmony.Parts[2].NotePhrases;

            _drums = chart.ProDrums.GetDifficulty(Difficulty.Expert);
            _guitar = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
            _bass = chart.FiveFretBass.GetDifficulty(Difficulty.Expert);
            _keys = chart.Keys.GetDifficulty(Difficulty.Expert);

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

            _vocalsNotes = GetAllNoteEvents(_vocals);
            _harmony0Notes = GetAllNoteEvents(_harmony0);
            _harmony1Notes = GetAllNoteEvents(_harmony1);
            _harmony2Notes = GetAllNoteEvents(_harmony2);
        }

        private int GuitarBassKeyboardEventChecker(InstrumentDifficulty<GuitarNote> instrument, ref int instrumentIndex)
        {
            int fretsPressed = 0;

            // Check if the index is within bounds
            if (instrumentIndex >= instrument.Notes.Count)
            {
                return 0; // No notes to process
            }

            var currentNote = instrument.Notes[instrumentIndex];

            // Handle sustained notes
            if (currentNote.Time < currentNote.TimeEnd && currentNote.TimeEnd <= GameManager.SongTime)
            {
                instrumentIndex++;
                return 0; // Sustain note has ended
            }

            // Handle instant notes
            if (!(currentNote.Time <= GameManager.SongTime)) return 0; // No notes currently active
            foreach (var note in currentNote.AllNotes)
            {
                fretsPressed |= (1 << note.Fret);
            }

            if (currentNote.Time == currentNote.TimeEnd)
            {
                // Note is instant, so it is done.
                instrumentIndex++;
            }

            return fretsPressed; // Return instant notes pressed
        }

        private int DrumsEventChecker(InstrumentDifficulty<DrumNote> instrument, ref int instrumentIndex)
        {
            int fretsPressed = 0;

            // Check if the index is within bounds
            if (instrumentIndex >= instrument.Notes.Count)
            {
                return 0; // No notes to process
            }

            var currentNote = instrument.Notes[instrumentIndex];

            // Handle sustained notes
            if (currentNote.Time < currentNote.TimeEnd && currentNote.TimeEnd <= GameManager.SongTime)
            {
                instrumentIndex++;
                return 0; // Sustain note has ended
            }

            // Handle instant notes
            if (!(currentNote.Time <= GameManager.SongTime)) return 0; // No notes currently active

            foreach (var note in currentNote.AllNotes)
            {
                fretsPressed |= (1 << note.Pad);
            }

            if (currentNote.Time == currentNote.TimeEnd)
            {
                // Note is instant
                instrumentIndex++;
            }

            return fretsPressed; // Return notes for sustain
        }

        private int VocalEventChecker(List<VocalNoteEvent> list, ref int listIndex)
        {
            if (listIndex < list.Count && list[listIndex].EndTime <= GameManager.SongTime)
            {
                listIndex++;
                return 0; // Note has ended
            }

            if (listIndex < list.Count && list[listIndex].StartTime <= GameManager.SongTime)
            {
                return (int) list[listIndex].Pitch;
            }

            return -1;
        }

        private void Update()
        {
            MasterLightingController.MLCPaused = GameManager.Paused;

            // Can't ref the CurrentXNotes properties.
            // Instrument events
            var DrumsNotes = DrumsEventChecker(_drums, ref _drumIndex);
            MasterLightingController.MLCCurrentDrumNotes = (byte)DrumsNotes;

            var GuitarNotes = GuitarBassKeyboardEventChecker(_guitar, ref _guitarIndex);
            MasterLightingController.MLCCurrentGuitarNotes = (byte)GuitarNotes;

            var BassNotes = GuitarBassKeyboardEventChecker(_bass, ref _bassIndex);
            MasterLightingController.MLCCurrentBassNotes = (byte)BassNotes;

            var KeysNotes = GuitarBassKeyboardEventChecker(_keys, ref _keysIndex);
            MasterLightingController.MLCCurrentKeysNotes = (byte)KeysNotes;

            // Vocal events
            var VocalNote = VocalEventChecker(_vocalsNotes, ref _vocalsIndex);
            if (VocalNote != -1)
            {
                MasterLightingController.MLCCurrentVocalNote = (byte)VocalNote;
            }

            var Harmony0Note = VocalEventChecker(_harmony0Notes, ref _harmony0Index);
            if (Harmony0Note != -1)
            {
                MasterLightingController.MLCCurrentHarmony0Note = (byte)Harmony0Note;
            }

            var Harmony1Note = VocalEventChecker(_harmony1Notes, ref _harmony1Index);
            if (Harmony1Note != -1)
            {
                MasterLightingController.MLCCurrentHarmony1Note = (byte)Harmony1Note;
            }

            var Harmony2Note = VocalEventChecker(_harmony2Notes, ref _harmony2Index);
            if (Harmony2Note != -1)
            {
                MasterLightingController.MLCCurrentHarmony2Note = (byte)Harmony2Note;
            }

            //Camera Cut events
            // NYI - waiting for parser rewrite

            // Performer events
            // NYI - waiting for parser rewrite

            // Post processing events
            while (_postProcessingIndex < Venue.PostProcessing.Count &&
                Venue.PostProcessing[_postProcessingIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.MLCPostProcessing = (byte) Venue.PostProcessing[_postProcessingIndex].Type;
                _postProcessingIndex++;
            }

            // Beatline events
            while (_syncIndex < _sync.Beatlines.Count && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.MLCCurrentBeat = _sync.Beatlines[_syncIndex].Type switch
                {
                    BeatlineType.Measure => 1,
                    BeatlineType.Strong  => 2,
                    BeatlineType.Weak    => 3,
                    _                    => 0,
                };

                _syncIndex++;
            }

            while (_bpmIndex < _sync.Tempos.Count && _sync.Tempos[_bpmIndex].Time <= GameManager.SongTime)
            {
                MasterLightingController.MLCCurrentBPM = Mathf.Round(_sync.Tempos[_bpmIndex].BeatsPerMinute);
                _bpmIndex++;
            }

            // The lighting cues from the venue track are handled here.
            while (LightingIndex < Venue.Lighting.Count && Venue.Lighting[LightingIndex].Time <= GameManager.SongTime)
            {
                switch (Venue.Lighting[LightingIndex].Type)
                {
                    case LightingType.Strobe_Off:
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Off;
                        break;

                    case LightingType.Strobe_Fast:
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Fast;
                        break;

                    case LightingType.Strobe_Medium:
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Medium;
                        break;

                    case LightingType.Strobe_Slow:
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Slow;
                        break;

                    case LightingType.Strobe_Fastest:
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Fastest;
                        break;

                    default:
                        // Okay so this a bit odd. The stage kit never has the strobe on with a lighting cue.
                        // But the Strobe_Off event is almost never used, relying instead on the cue change to turn it off.
                        // So this technically should be in the stage kit lighting controller code but I don't want the
                        // stage kit reaching into this main lighting controller.So we'll just turn it off here.
                        MasterLightingController.MLCStrobeState = (byte)StageKitStrobeSpeed.Off;
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
                    MasterLightingController.MLCFogState = true;
                }
                else if (Venue.Stage[_stageIndex].Effect == StageEffect.FogOff)
                {
                    MasterLightingController.MLCFogState = false;
                }
                else if (Venue.Stage[_stageIndex].Effect == StageEffect.BonusFx)
                {
                    MasterLightingController.MLCBonusFX = true;
                }

                _stageIndex++;
            }
        }

        private List<VocalNoteEvent> GetAllNoteEvents(List<VocalsPhrase> vocalPhrases)
        {
            var allNoteEvents = new List<VocalNoteEvent>();

            foreach (var phrase in vocalPhrases)
            {
                foreach (var childNote in phrase.PhraseParentNote.ChildNotes)
                {
                    allNoteEvents.Add(new VocalNoteEvent
                    {
                        Pitch = (int) childNote.Pitch,
                        StartTime = childNote.Time,
                        EndTime = childNote.TimeEnd,
                    });

                    foreach (var grandChildNote in childNote.ChildNotes)
                    {
                        allNoteEvents.Add(new VocalNoteEvent
                        {
                            Pitch = (int) grandChildNote.Pitch,
                            StartTime = grandChildNote.Time,
                            EndTime = grandChildNote.TimeEnd,
                        });
                    }
                }
            }

            // Sort events by time to ensure correct order
            allNoteEvents.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            return allNoteEvents;
        }
    }
}