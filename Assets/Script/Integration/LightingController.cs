using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using Random = UnityEngine.Random;

namespace YARG.Integration
{
    public class LightingController : MonoBehaviour
    {
        public static LightingEvent CurrentLightingCue
        {
            get => _currentLightingCue;
            set
            {
                OnLightingEvent?.Invoke(value);
                _currentLightingCue = value;
            }
        }

        public static DrumNote CurrentDrumNote
        {
            get => _currentDrumNote;
            set
            {
                OnDrumEvent?.Invoke(value);
                _currentDrumNote = value;
            }
        }

        public static VocalNote CurrentVocalNote
        {
            get => _currentVocalNote;
            set
            {
                OnVocalsEvent?.Invoke(value);
                _currentVocalNote = value;
            }
        }

        public static Beatline CurrentBeatline
        {
            get => _currentBeatline;
            set
            {
                OnBeatLineEvent?.Invoke(value);
                _currentBeatline = value;
            }
        }

        public static StageEffectEvent CurrentStageEffect
        {
            get => _currentStageEffect;
            set
            {
                OnStageEffectEvent?.Invoke(value);
                _currentStageEffect = value;
            }
        }

        public static bool Paused
        {
            get => _paused;
            set
            {
                OnPause?.Invoke(value);
                _paused = value;
            }
        }

        public static bool LargeVenue
        {
            get => _largeVenue;
            set
            {
                OnLargeVenue?.Invoke(value);
                _largeVenue = value;
            }
        }

        public static event Action<bool> OnLargeVenue;
        public static event Action<bool> OnPause;
        public static event Action<DrumNote> OnDrumEvent;
        public static event Action<VocalNote> OnVocalsEvent;
        public static event Action<Beatline> OnBeatLineEvent;
        public static event Action<LightingEvent> OnLightingEvent;
        public static event Action<StageEffectEvent> OnStageEffectEvent;

        private static LightingEvent _currentLightingCue;
        private static DrumNote _currentDrumNote;
        private static VocalNote _currentVocalNote;
        private static Beatline _currentBeatline;
        private static StageEffectEvent _currentStageEffect;
        private static bool _paused;
        private static bool _largeVenue;

        private GameplayBehaviour _gameplayMonitor;

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            switch (scene.buildIndex)
            {
                case (int) SceneIndex.Gameplay:
                    Destroy(_gameplayMonitor);
                    break;

                case (int) SceneIndex.Score:
                    break;

                case (int) SceneIndex.Menu:
                    break;

                default:
                    Debug.LogWarning("(Lighting Controller) Unknown Scene unloaded!");
                    break;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.buildIndex)
            {
                case (int) SceneIndex.Gameplay:
                    _gameplayMonitor = gameObject.AddComponent<GameplayLightingMonitor>();
                    break;

                case (int) SceneIndex.Score:
                    CurrentLightingCue = new LightingEvent(LightingType.Score, 0, 0);

                    break;
                case (int) SceneIndex.Menu:
                    CurrentLightingCue = new LightingEvent(LightingType.Menu, 0, 0);
                    break;

                default:
                    Debug.LogWarning("(Lighting Controller) Unknown Scene loaded!");
                    break;
            }
        }
    }

    public class GameplayLightingMonitor : GameplayBehaviour
    {
        private VenueTrack _venue;
        private SyncTrack _sync;
        private List<VocalsPhrase> _vocals;
        private InstrumentDifficulty<DrumNote> _drums;
        private int _eventIndex;
        private int _lightingIndex;
        private int _syncIndex;
        private int _vocalsIndex;
        private int _drumIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            //This should be read from the venue itself eventually, but for now, we'll just randomize it.
            LightingController.LargeVenue = Random.Range(0, 1) == 1;
            _venue = chart.VenueTrack;
            _sync = chart.SyncTrack;
            _vocals = chart.Vocals.Parts[0].NotePhrases;
            chart.FourLaneDrums.Difficulties.TryGetValue(Difficulty.Expert, out _drums);
        }

        private void Update()
        {
            if (LightingController.Paused != GameManager.Paused)
            {
                LightingController.Paused = GameManager.Paused;
            }

            //drum events
            while (_drumIndex < _drums.Notes.Count && _drums.Notes[_drumIndex].Time <= GameManager.SongTime)
            {
                LightingController.CurrentDrumNote = _drums.Notes[_drumIndex];
                _drumIndex++;
            }

            //End of vocal phrase. SilhouetteSpot is the only cue that uses vocals, listening to the end of the phrase.
            while (_vocalsIndex < _vocals.Count &&  Math.Min(_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1].TotalTimeEnd, _vocals[_vocalsIndex].TimeEnd)   <= GameManager.SongTime)
            {
                LightingController.CurrentVocalNote =_vocals[_vocalsIndex].PhraseParentNote.ChildNotes[^1];
                _vocalsIndex++;
            }

            //beatline events
            while (_syncIndex < _sync.Beatlines.Count && _sync.Beatlines[_syncIndex].Time <= GameManager.SongTime)
            {
                LightingController.CurrentBeatline = _sync.Beatlines[_syncIndex];
                _syncIndex++;
            }

            //The lighting cues from the venue track are handled here.
            while (_lightingIndex < _venue.Lighting.Count && _venue.Lighting[_lightingIndex].Time <= GameManager.SongTime)
            {
                LightingController.CurrentLightingCue = _venue.Lighting[_lightingIndex];
                _lightingIndex++;
            }

            //For "fogOn", "fogOff", and "BonusFx" events
            while (_eventIndex < _venue.Stage.Count && _venue.Stage[_eventIndex].Time <= GameManager.SongTime)
            {
                LightingController.CurrentStageEffect = _venue.Stage[_eventIndex];
                _eventIndex++;
            }
        }
    }
}