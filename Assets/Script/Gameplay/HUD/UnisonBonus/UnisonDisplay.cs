using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Logging;
using YARG.Localization;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum UnisonDisplaySetting
    {
        Always,
        MultiplayerOnly, // Technically not what it does, but simpler to explain than multiple unison participants
        Disabled,
    }

    public class UnisonDisplay : GameplayBehaviour
    {
        private const double TRANSITION_DURATION = 0.2;
        private const double DISPLAY_HOLD_TIME   = 1.5;
        private const double DISPLAY_PRE_TIME    = 0.5;

        [SerializeField]
        private GameObject _parent;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private UnisonIconGroup _iconContainer;
        [SerializeField]
        private UnisonBar _unisonBar;
        [SerializeField]
        private Image _backgroundImage;
        [SerializeField]
        private Sprite _defaultSprite;
        [SerializeField]
        private Sprite _successSprite;
        [SerializeField]
        private Sprite _failSprite;

        [SerializeField]
        private Color _completeColor = new(0.988f, 0.835f, 0.282f, 1f);
        [SerializeField]
        private Color _failColor = new(0.953f, 0.169f, 0.216f, 1f);

        private readonly List<UnisonPhraseData> _phrases = new();

        private readonly Dictionary<int, EngineUnisonState> _unisonState = new();
        private          BaseUnisonObject                   _activeUnisonObject;

        private Sequence _completeSequence;
        private int      _currentPhraseIndex;
        private bool     _isEditMode;

        private double _lastVisualTime;

        private void Update()
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                gameObject.SetActive(false);
                return;
            }

            var currentPhrase = _phrases[_currentPhraseIndex];
            double time = GameManager.VisualTime;

            if (_lastVisualTime > time)
            {
                return; // Prevent weirdness when rewinding
            }

            _lastVisualTime = time;

            if (time > currentPhrase.TransitionOut.TimeEnd)
            {
                AdvanceToNextPhrase();
                return;
            }

            UpdateScale(currentPhrase, time);
        }

        protected override void OnSongStarted()
        {
            int minPlayers = SettingsManager.Settings.UnisonDisplay.Value switch
            {
                UnisonDisplaySetting.Always          => 1,
                UnisonDisplaySetting.MultiplayerOnly => 2,
                UnisonDisplaySetting.Disabled        => int.MaxValue,
                _                                    => throw new ArgumentOutOfRangeException(),
            };

            if (SettingsManager.Settings.UnisonDisplay.Value == UnisonDisplaySetting.Disabled ||
                GameManager.EngineManager.Engines.Count(e =>
                    e.Instrument is not Instrument.Vocals and not Instrument.Harmony) < minPlayers)
            {
                gameObject.SetActive(false);
                return;
            }

            InitializePhrases(minPlayers);

            if (_phrases.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _headerText.text = Localize.Key("Gameplay.UnisonDisplay.Header");

            BuildTransitionTimings(GameManager.SongSpeed);

            _parent.SetActive(false);
            SetDisplayType(_phrases[0].Event.PartCount);
            _completeSequence = BuildCompleteSequence(gameObject);

            foreach (var engineContainer in GameManager.EngineManager.Engines)
            {
                if (engineContainer.UnisonPhrases.Count == 0)
                {
                    continue;
                }

                _unisonState[engineContainer.EngineId] = new EngineUnisonState();
                _iconContainer.InitializeIcon(engineContainer.EngineId, engineContainer.GetInstrumentSprite());
                SubscribeToEngineEvents(engineContainer);
            }

            _activeUnisonObject.SetParticipants(_phrases[0].Event.ParticipantIds);
        }

        public static Sequence BuildCompleteSequence(GameObject target) =>
            DOTween.Sequence()
                .Append(target.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutSine))
                .Append(target.transform.DOScale(1f, 0.2f).SetEase(Ease.OutSine))
                .Pause().SetLink(target).SetAutoKill(false);

        private void InitializePhrases(int minPlayers)
        {
            var rawEvents = GameManager.EngineManager.UnisonEvents
                .Where(e => e.PartCount >= minPlayers)
                .OrderBy(e => e.Time)
                .ToList();

            double maxTime = 0;
            EngineManager.UnisonEvent maxTimeEvent = null;
            foreach (var unisonEvent in rawEvents)
            {
                if (unisonEvent.Time < maxTime)
                {
                    string eventParticipants = unisonEvent.ParticipantIds
                        .Aggregate("", (current, id) => current + (id + ", ")).TrimEnd(',', ' ');
                    string maxEventParticipants = maxTimeEvent!.ParticipantIds
                        .Aggregate("", (current, id) => current + (id + ", ")).TrimEnd(',', ' ');
                    YargLogger.LogFormatWarning<double, double, string, double, double, string>(
                        "Removed overlapping unison event: engines {2} from {0} - {1} overlapped with engines {5} from {3} - {4}",
                        unisonEvent.Time, unisonEvent.TimeEnd, eventParticipants, maxTimeEvent!.Time,
                        maxTimeEvent!.TimeEnd, maxEventParticipants);
                }
                else
                {
                    if (unisonEvent.Time > maxTime)
                    {
                        maxTime = unisonEvent.TimeEnd;
                        maxTimeEvent = unisonEvent;
                    }

                    _phrases.Add(new UnisonPhraseData
                    {
                        Event = unisonEvent,
                    });
                }
            }
        }

        private void BuildTransitionTimings(double songSpeedMultiplier)
        {
            for (int i = 0; i < _phrases.Count; i++)
            {
                var currentPhrase = _phrases[i];
                var unisonEvent = currentPhrase.Event;

                if (i == 0)
                {
                    double startTime = Math.Max(0,
                        unisonEvent.Time - (DISPLAY_PRE_TIME + TRANSITION_DURATION) * songSpeedMultiplier);
                    double endTime = Math.Max(0, unisonEvent.Time - DISPLAY_PRE_TIME * songSpeedMultiplier);
                    currentPhrase.TransitionIn = new TransitionTiming(startTime, endTime);
                }

                if (i == _phrases.Count - 1)
                {
                    double endTime = Math.Min(GameManager.SongLength,
                        unisonEvent.TimeEnd + (DISPLAY_HOLD_TIME + TRANSITION_DURATION) * songSpeedMultiplier);
                    currentPhrase.TransitionOut =
                        new TransitionTiming(endTime - TRANSITION_DURATION * songSpeedMultiplier, endTime);
                }
                else
                {
                    var nextPhrase = _phrases[i + 1];
                    var nextUnison = nextPhrase.Event;

                    if (nextUnison.Time - unisonEvent.TimeEnd <
                        (2 * TRANSITION_DURATION + DISPLAY_HOLD_TIME + DISPLAY_PRE_TIME) * songSpeedMultiplier)
                    {
                        double totalTime = nextUnison.Time - unisonEvent.TimeEnd;
                        double holdTime = totalTime * 0.25;
                        double preTime = totalTime * 0.25;
                        double transitionTime = totalTime * 0.25;

                        currentPhrase.TransitionOut = new TransitionTiming(
                            unisonEvent.TimeEnd + holdTime,
                            unisonEvent.TimeEnd + holdTime + transitionTime);

                        nextPhrase.TransitionIn = new TransitionTiming(
                            nextUnison.Time - transitionTime - preTime,
                            nextUnison.Time - preTime);
                    }
                    else
                    {
                        currentPhrase.TransitionOut = new TransitionTiming(
                            unisonEvent.TimeEnd + DISPLAY_HOLD_TIME * songSpeedMultiplier,
                            unisonEvent.TimeEnd + (TRANSITION_DURATION + DISPLAY_HOLD_TIME) * songSpeedMultiplier);

                        nextPhrase.TransitionIn = new TransitionTiming(
                            nextUnison.Time - (TRANSITION_DURATION + DISPLAY_PRE_TIME) * songSpeedMultiplier,
                            nextUnison.Time - DISPLAY_PRE_TIME * songSpeedMultiplier);
                    }
                }
            }
        }

        public void SetSongTime(double time)
        {
            _currentPhraseIndex = 0;
            while (_currentPhraseIndex < _phrases.Count && time > _phrases[_currentPhraseIndex].TransitionOut.TimeEnd)
            {
                _currentPhraseIndex++;
            }
        }

        private void SubscribeToEngineEvents(EngineManager.EngineContainer engineContainer)
        {
            int engineId = engineContainer.EngineId;

            switch (engineContainer.Engine)
            {
                case GuitarEngine guitarEngine:
                    guitarEngine.OnNoteHit += (_, note) => OnNoteHit(engineId, note);
                    guitarEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                        OnStarPowerPhraseStart(note, noteCount, engineId);
                    guitarEngine.OnStarPowerPhraseMissed += note => OnStarPowerPhraseMissed(note, engineId);
                    break;
                case DrumsEngine drumEngine:
                    drumEngine.OnNoteHit += (_, note) => OnNoteHit(engineId, note);
                    drumEngine.OnStarPowerPhraseStart +=
                        (note, noteCount) => OnStarPowerPhraseStart(note, noteCount, engineId);
                    drumEngine.OnStarPowerPhraseMissed += note => OnStarPowerPhraseMissed(note, engineId);
                    break;
                case KeysEngine<ProKeysNote> proKeysEngine:
                    proKeysEngine.OnNoteHit += (_, note) => OnNoteHit(engineId, note);
                    proKeysEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                        OnStarPowerPhraseStart(note, noteCount, engineId);
                    proKeysEngine.OnStarPowerPhraseMissed += note => OnStarPowerPhraseMissed(note, engineId);
                    break;
                case KeysEngine<GuitarNote> fiveLaneKeysEngine:
                    fiveLaneKeysEngine.OnNoteHit += (_, note) => OnNoteHit(engineId, note);
                    fiveLaneKeysEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                        OnStarPowerPhraseStart(note, noteCount, engineId);
                    fiveLaneKeysEngine.OnStarPowerPhraseMissed += note => OnStarPowerPhraseMissed(note, engineId);
                    break;
            }
        }

        private void OnStarPowerPhraseStart(ChartEvent note, int noteCount, int engineId)
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            var currentPhrase = _phrases[_currentPhraseIndex].Event;
            if (!currentPhrase.ParticipantIds.Contains(engineId))
            {
                return;
            }

            var unisonState = _unisonState[engineId];

            unisonState.NotesInCurrentPhrase = noteCount;
            unisonState.NotesHitInCurrentPhrase = 0;
        }

        private void OnStarPowerPhraseMissed(ChartEvent note, int engineId)
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            var currentPhrase = _phrases[_currentPhraseIndex].Event;
            if (!currentPhrase.ParticipantIds.Contains(engineId))
            {
                return;
            }

            if (note.Time >= currentPhrase.Time && note.Time <= currentPhrase.TimeEnd)
            {
                _unisonState[engineId].HasFailedCurrentPhrase = true;
                _headerText.color = _failColor;
                _backgroundImage.sprite = _failSprite;
                _activeUnisonObject.FailUnison(engineId);
            }
        }

        private void OnNoteHit(int engineId, ChartEvent note)
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            var currentPhrase = _phrases[_currentPhraseIndex].Event;

            if (note.Time < currentPhrase.Time ||
                note.Time > currentPhrase.TimeEnd ||
                !currentPhrase.ParticipantIds.Contains(engineId) ||
                !_unisonState.TryGetValue(engineId, out var unisonState) ||
                unisonState.NotesHitInCurrentPhrase >= unisonState.NotesInCurrentPhrase ||
                unisonState.HasFailedCurrentPhrase)
            {
                return;
            }

            unisonState.NotesHitInCurrentPhrase++;
            SetProgress(engineId);
        }

        public void OnUnisonPhraseSuccess()
        {
            _headerText.color = _completeColor;
            _backgroundImage.sprite = _successSprite;
            _completeSequence.Restart();
        }

        private void UpdateScale(UnisonPhraseData currentPhrase, double time)
        {
            if (time < currentPhrase.TransitionIn.Time)
            {
                return;
            }

            if (!_parent.activeSelf)
            {
                _parent.SetActive(true);
            }

            float scale;
            if (time <= currentPhrase.TransitionIn.TimeEnd)
            {
                float progress = currentPhrase.TransitionIn.Progress(time);
                scale = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutSine);
            }
            else if (time < currentPhrase.TransitionOut.Time)
            {
                scale = 1f;
            }
            else
            {
                float progress = currentPhrase.TransitionOut.Progress(time);
                scale = DOVirtual.EasedValue(1f, 0f, progress, Ease.OutSine);
            }

            _parent.transform.localScale = new Vector3(scale, scale, 1f);

            if (time > currentPhrase.TransitionOut.TimeEnd && _parent.activeSelf)
            {
                _parent.SetActive(false);
            }
        }

        private void AdvanceToNextPhrase()
        {
            _currentPhraseIndex++;
            if (_currentPhraseIndex >= _phrases.Count)
            {
                gameObject.SetActive(false);
                return;
            }

            _headerText.color = Color.white;
            _backgroundImage.sprite = _defaultSprite;
            var nextPhrase = _phrases[_currentPhraseIndex].Event;

            SetDisplayType(nextPhrase.PartCount);
            _activeUnisonObject.SetParticipants(nextPhrase.ParticipantIds);

            foreach (int engineId in _unisonState.Keys.ToList())
            {
                var unisonState = _unisonState[engineId];
                unisonState.NotesHitInCurrentPhrase = 0;
                unisonState.HasFailedCurrentPhrase = false;
            }
        }

        private void SetDisplayType(int participantCount)
        {
            if (participantCount > 8)
            {
                _iconContainer.gameObject.SetActive(false);
                _unisonBar.gameObject.SetActive(true);
                _activeUnisonObject = _unisonBar;
            }
            else
            {
                _iconContainer.gameObject.SetActive(true);
                _unisonBar.gameObject.SetActive(false);
                _activeUnisonObject = _iconContainer;
            }
        }

        private void SetProgress(int engineId)
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            var currentEvent = _phrases[_currentPhraseIndex].Event;
            if (!currentEvent.ParticipantIds.Contains(engineId))
            {
                return;
            }

            var unisonEvent = _unisonState[engineId];
            float progress = unisonEvent.NotesInCurrentPhrase > 0
                ? unisonEvent.NotesHitInCurrentPhrase / (float) unisonEvent.NotesInCurrentPhrase
                : 0f;

            _activeUnisonObject.SetProgress(engineId, progress);
        }

        public void OnEditModeChanged()
        {
            _isEditMode = !_isEditMode;
            if (_isEditMode)
            {
                SetDisplayType(1);
                _activeUnisonObject.SetParticipants(new List<int>
                {
                    0, // the first engine *should* always be 0
                });

                _parent.SetActive(true);
                _parent.transform.localScale = Vector3.one;
            }
            else
            {
                _parent.SetActive(false);
                _parent.transform.localScale = Vector3.zero;
            }
        }

        private readonly struct TransitionTiming
        {
            public readonly double Time;
            public readonly double TimeEnd;

            public TransitionTiming(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
            }

            public float Progress(double time)
            {
                double length = TimeEnd - Time;
                if (length <= 0)
                {
                    return 1f;
                }

                return Mathf.Clamp01((float) ((time - Time) / length));
            }
        }

        private class UnisonPhraseData
        {
            public EngineManager.UnisonEvent Event;
            public TransitionTiming          TransitionIn;
            public TransitionTiming          TransitionOut;
        }

        private class EngineUnisonState
        {
            public bool HasFailedCurrentPhrase;
            public int  NotesHitInCurrentPhrase;
            public int  NotesInCurrentPhrase;
        }
    }
}