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
        private const double TRANSITION_DURATION               = 0.2;
        private const double DISPLAY_HOLD_TIME                 = 1.5;
        private const double DISPLAY_PRE_TIME                  = 0.2;
        private const int    MAX_PARTICIPANTS_FOR_ICON_DISPLAY = 8;

        private readonly List<UnisonPhraseData> _phrases = new();

        private readonly Dictionary<int, EngineUnisonState> _unisonState        = new();
        private readonly List<Action>                       _unsubscribeActions = new();
        private          BaseUnisonObject                   _activeUnisonObject;
        private          Sequence                           _completeSequence;
        private          int                                _currentPhraseIndex;
        private          bool                               _isEditMode;
        private          double                             _lastVisualTime;

        [SerializeField]
        private Image _backgroundImage;
        [SerializeField]
        private Sprite _defaultSprite;
        [SerializeField]
        private Sprite _failSprite;
        [SerializeField]
        private Color _failColor;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private UnisonIconGroup _iconContainer;
        [SerializeField]
        private UnisonBar _unisonBar;
        [SerializeField]
        private GameObject _parent;
        [SerializeField]
        private Sprite _successSprite;
        [Header("Position Offsets")]
        [Tooltip(
            "Positions for the display based on the number of tracks in the song. Index 0 is ignored as the display is draggable in singleplayer.")]
        [SerializeField]
        private List<Vector2> _trackCountToPositionOffset;

        [Tooltip(
            "Positions for the display based on the number of tracks in the song, when a vocals player is present.")]
        [SerializeField]
        private List<Vector2> _trackCountToPositionOffsetWithVocals;

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
                _currentPhraseIndex++;
                YargLogger.LogFormatTrace("Advancing to unison phrase {0}", _currentPhraseIndex);
                ResetState();
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

            PositionDisplay();

            _headerText.text = Localize.Key("Gameplay.UnisonDisplay.Header");

            BuildTransitionTimings(GameManager.SongSpeed);

            _parent.SetActive(false);

            int engineCount = GameManager.EngineManager.Engines.Count;
            if (engineCount > MAX_PARTICIPANTS_FOR_ICON_DISPLAY)
            {
                _unisonBar.Initialize(engineCount);
            }
            _iconContainer.Initialize(engineCount);

            SetDisplayType(_phrases[0].Event.PartCount);
            _activeUnisonObject.ResetState();
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
                if (!_phrases[0].Event.ParticipantToPhrase.TryGetValue(engineContainer.EngineId, out var phrase))
                {
                    continue;
                }

                _activeUnisonObject.AddParticipant(engineContainer.EngineId, phrase.NoteCount);
            }
        }

        private void PositionDisplay()
        {
            if (GameManager.Players.Count < 2)
            {
                return;
            }

            int trackCount = 0;
            bool vocalsPresent = false;
            foreach (var engineContainer in GameManager.EngineManager.Engines)
            {
                if (engineContainer.Instrument is Instrument.Vocals or Instrument.Harmony)
                {
                    vocalsPresent = true;
                }
                else
                {
                    trackCount++;
                }
            }

            transform.localPosition += (Vector3) (vocalsPresent
                ? _trackCountToPositionOffsetWithVocals[
                    Mathf.Clamp(trackCount - 1, 0, _trackCountToPositionOffsetWithVocals.Count - 1)]
                : _trackCountToPositionOffset[Mathf.Clamp(trackCount - 1, 0, _trackCountToPositionOffset.Count - 1)]);
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
                    string eventParticipants = unisonEvent.ParticipantToPhrase.Keys
                        .Aggregate("", (current, id) => current + (id + ", ")).TrimEnd(',', ' ');
                    string maxEventParticipants = maxTimeEvent!.ParticipantToPhrase.Keys
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
            if (time > _phrases.Last().Event.TimeEnd)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _currentPhraseIndex = 0;
            _lastVisualTime = time;
            while (_currentPhraseIndex < _phrases.Count && time > _phrases[_currentPhraseIndex].Event.TimeEnd)
            {
                _currentPhraseIndex++;
            }

            ResetState();
        }

        private void SubscribeToEngineEvents(EngineManager.EngineContainer engineContainer)
        {
            int engineId = engineContainer.EngineId;

            switch (engineContainer.Engine)
            {
                case GuitarEngine guitarEngine:
                    GuitarEngine.NoteHitEvent guitarNoteHit =
                        (_, note) =>
                        {
                            OnNoteHit(engineId, note, false);
                        };
                    guitarEngine.OnNoteHit += guitarNoteHit;
                    _unsubscribeActions.Add(() => guitarEngine.OnNoteHit -= guitarNoteHit);

                    GuitarEngine.StarPowerPhraseMissEvent
                        guitarStarPowerMissed =
                            note => OnStarPowerPhraseMissed(note, engineId);
                    guitarEngine.OnStarPowerPhraseMissed += guitarStarPowerMissed;
                    _unsubscribeActions.Add(() => guitarEngine.OnStarPowerPhraseMissed -= guitarStarPowerMissed);
                    break;
                case DrumsEngine drumsEngine:
                    DrumsEngine.NoteHitEvent drumsNoteHit = (_, note) =>
                    {
                        OnNoteHit(engineId, note, true);
                    };
                    drumsEngine.OnNoteHit += drumsNoteHit;
                    _unsubscribeActions.Add(() => drumsEngine.OnNoteHit -= drumsNoteHit);

                    DrumsEngine.StarPowerPhraseMissEvent
                        drumsStarPowerMissed =
                            note => OnStarPowerPhraseMissed(note, engineId);
                    drumsEngine.OnStarPowerPhraseMissed += drumsStarPowerMissed;
                    _unsubscribeActions.Add(() => drumsEngine.OnStarPowerPhraseMissed -= drumsStarPowerMissed);
                    break;
                case KeysEngine<ProKeysNote> proKeysEngine:
                    KeysEngine<ProKeysNote>.NoteHitEvent proKeysNoteHit = (_, note) =>
                    {
                        OnNoteHit(engineId, note, false);
                    };
                    proKeysEngine.OnNoteHit += proKeysNoteHit;
                    _unsubscribeActions.Add(() => proKeysEngine.OnNoteHit -= proKeysNoteHit);

                    KeysEngine<ProKeysNote>.StarPowerPhraseMissEvent
                        proKeysStarPowerMissed =
                            note => OnStarPowerPhraseMissed(note, engineId);
                    proKeysEngine.OnStarPowerPhraseMissed += proKeysStarPowerMissed;
                    _unsubscribeActions.Add(() => proKeysEngine.OnStarPowerPhraseMissed -= proKeysStarPowerMissed);
                    break;
                case KeysEngine<GuitarNote> fiveLaneKeysEngine:
                    KeysEngine<GuitarNote>.NoteHitEvent fiveLaneKeysNoteHit =
                        (_, note) =>
                        {
                            OnNoteHit(engineId, note, false);
                        };
                    fiveLaneKeysEngine.OnNoteHit += fiveLaneKeysNoteHit;
                    _unsubscribeActions.Add(() => fiveLaneKeysEngine.OnNoteHit -= fiveLaneKeysNoteHit);

                    KeysEngine<GuitarNote>.StarPowerPhraseMissEvent
                        fiveLaneKeysStarPowerMissed =
                            note => OnStarPowerPhraseMissed(note, engineId);
                    fiveLaneKeysEngine.OnStarPowerPhraseMissed += fiveLaneKeysStarPowerMissed;
                    _unsubscribeActions.Add(() =>
                        fiveLaneKeysEngine.OnStarPowerPhraseMissed -= fiveLaneKeysStarPowerMissed);
                    break;
            }
        }

        private void OnStarPowerPhraseMissed<T>(T note, int engineId) where T : Note<T>
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            var currentPhrase = _phrases[_currentPhraseIndex].Event;
            if (!currentPhrase.ParticipantToPhrase.ContainsKey(engineId) || note.Time < currentPhrase.Time ||
                note.Time > currentPhrase.TimeEnd)
            {
                return;
            }

            YargLogger.LogFormatTrace("Engine {0} failed a unison phrase at time {1}", engineId, note.Time);
            _unisonState[engineId].HasFailedCurrentPhrase = true;
            _headerText.color = _failColor;
            _backgroundImage.sprite = _failSprite;
            _activeUnisonObject.FailUnison(engineId);
        }

        private void OnNoteHit<T>(int engineId, T note, bool includeChildNotes) where T : Note<T>
        {
            if (_currentPhraseIndex >= _phrases.Count)
            {
                return;
            }

            if (!_phrases[_currentPhraseIndex].Event.ParticipantToPhrase.TryGetValue(engineId, out var currentPhrase) ||
                note.Time < currentPhrase.Time ||
                note.Time > currentPhrase.TimeEnd ||
                (note.IsChild && !includeChildNotes) ||
                !note.IsStarPower ||
                !_unisonState.TryGetValue(engineId, out var unisonState) ||
                unisonState.HasFailedCurrentPhrase)
            {
                return;
            }

            YargLogger.LogFormatTrace("Engine {0} hit a note in a unison phrase at time {1}", engineId, note.Time);

            unisonState.NotesHitInCurrentPhrase++;
            SetProgress(engineId);
        }

        public void OnUnisonPhraseSuccess()
        {
            YargLogger.LogTrace("Unison phrase completed successfully");
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

        private void ResetState()
        {

            if (_currentPhraseIndex >= _phrases.Count)
            {
                gameObject.SetActive(false);
                return;
            }

            _headerText.color = Color.white;
            _backgroundImage.sprite = _defaultSprite;
            var nextPhrase = _phrases[_currentPhraseIndex].Event;

            SetDisplayType(nextPhrase.PartCount);
            _activeUnisonObject.ResetState();
            foreach ((int participantId, var phrase) in nextPhrase.ParticipantToPhrase)
            {
                _activeUnisonObject.AddParticipant(participantId, phrase.NoteCount);
                if (!_unisonState.TryGetValue(participantId, out var unisonState))
                {
                    return;
                }

                unisonState.NotesHitInCurrentPhrase = 0;
                unisonState.HasFailedCurrentPhrase = false;
            }
        }

        private void SetDisplayType(int participantCount)
        {
            if (participantCount > MAX_PARTICIPANTS_FOR_ICON_DISPLAY)
            {
                YargLogger.LogTrace("Setting display mode to bar");
                _iconContainer.gameObject.SetActive(false);
                _unisonBar.gameObject.SetActive(true);
                _activeUnisonObject = _unisonBar;
            }
            else
            {
                YargLogger.LogTrace("Setting display mode to icons");
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
            if (!currentEvent.ParticipantToPhrase.TryGetValue(engineId, out var participant))
            {
                return;
            }

            var unisonEvent = _unisonState[engineId];
            YargLogger.LogFormatTrace("Engine {0} progress in unison phrase: {1}/{2}", engineId,
                unisonEvent.NotesHitInCurrentPhrase, participant.NoteCount);
            _activeUnisonObject.SetNotesHit(engineId, unisonEvent.NotesHitInCurrentPhrase);
        }

        public void OnEditModeChanged()
        {
            _isEditMode = !_isEditMode;
            var unisonPhrase = _phrases[_currentPhraseIndex];
            if (unisonPhrase.TransitionIn.Time <= GameManager.VisualTime && GameManager.VisualTime <= unisonPhrase.TransitionOut.TimeEnd)
            {
                // There's already an active unison phrase, don't do anything.
                return;
            }
            if (_isEditMode)
            {
                SetDisplayType(1);
                _activeUnisonObject.ResetState();
                _activeUnisonObject.AddParticipant(0, 69); // Add a dummy participant for edit mode
                _parent.SetActive(true);
                _parent.transform.localScale = Vector3.one;
            }
            else
            {
                _parent.SetActive(false);
                _parent.transform.localScale = Vector3.zero;
                ResetState();
            }
        }

        protected override void GameplayDestroy()
        {
            foreach (var unsubAction in _unsubscribeActions)
            {
                unsubAction();
            }

            _unsubscribeActions.Clear();
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
        }
    }
}