using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum UnisonDisplaySetting
    {
        Always,
        MultiplayerOnly, // Technically not what it does, but simpler to explain than multiple unison participants
        Disabled
    }

    public class UnisonDisplay : GameplayBehaviour
    {
        private readonly struct TransitionTiming
        {
            public readonly double Time;
            public readonly double TimeEnd;

            public TransitionTiming(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
            }

            public float Progress(double time) => Mathf.Clamp01((float) ((time - Time) / (TimeEnd - Time)));
        }

        [SerializeField]
        private GameObject _parent;
        [SerializeField]
        private GameObject _iconContainer;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private UnisonIcon _instrumentIconPrefab;
        [SerializeField]
        private UnisonBar _unisonBar;

        private Sequence _completeSequence;

        private TransitionTiming[] _transitionTimes;
        private int                _transitionIndex;

        private bool  _isEditMode;

        private readonly Dictionary<int, UnisonIcon> _instrumentIcons = new();

        private const double TRANSITION_DURATION = 0.2;
        private const double DISPLAY_HOLD_TIME   = 1.5;
        private const double DISPLAY_PRE_TIME    = 0.5;

        private readonly Dictionary<int, int>            _engineIdToNotesInUnisonPhrase   = new();
        private readonly Dictionary<int, int>            _engineIdToNoteHitInUnisonPhrase = new();
        private readonly List<EngineManager.UnisonEvent> _unisonEvents                    = new();
        private          int                             _unisonEventIndex;

        protected override void OnSongStarted()
        {
            int minPlayers = SettingsManager.Settings.UnisonDisplay.Value switch
            {
                UnisonDisplaySetting.Always          => 1,
                UnisonDisplaySetting.MultiplayerOnly => 2,
                UnisonDisplaySetting.Disabled        => int.MaxValue,
                _                                    => throw new ArgumentOutOfRangeException()
            };
            if (SettingsManager.Settings.UnisonDisplay.Value == UnisonDisplaySetting.Disabled ||
                GameManager.EngineManager.Engines.Count(e =>
                    e.Instrument is not Instrument.Vocals and not Instrument.Harmony) < minPlayers)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (var unisonEvent in GameManager.EngineManager.UnisonEvents)
            {
                if (unisonEvent.PartCount >= minPlayers)
                {
                    _unisonEvents.Add(unisonEvent);
                }
            }

            if (_unisonEvents.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _unisonEvents.Sort((a, b) => a.Time.CompareTo(b.Time)); // Apparently they aren't sorted?

            double maxTime = 0;
            int index = 0;
            while (index < _unisonEvents.Count)
            {
                var unisonEvent = _unisonEvents[index];
                if (unisonEvent.Time < maxTime)
                {
                    _unisonEvents.RemoveAt(index);
                    YargLogger.LogFormatDebug("Removed overlapping unison event: {2} players from {0} - {1}", unisonEvent.Time,
                        unisonEvent.TimeEnd, unisonEvent.PartCount);
                }
                else
                {
                    maxTime = Math.Max(maxTime, unisonEvent.TimeEnd);
                    index++;
                }
            }

            foreach (var unisonEvent in _unisonEvents)
            {
                YargLogger.LogFormatDebug<double, double, int>("Unison Event: {2} players from {0} - {1}", unisonEvent.Time,
                    unisonEvent.TimeEnd, unisonEvent.PartCount);
            }

            BuildTransitionTimings(GameManager.SongSpeed);
            for (int i = 0; i < _transitionTimes.Length; i++)
            {
                var time = _transitionTimes[i];
                YargLogger.LogFormatDebug<double, double, string>("Unison Display Transition Timing: {2} from {0} - {1}", time.Time,
                    time.TimeEnd, i % 2 == 0 ? "In" : "Out");
            }

            _parent.SetActive(false);
            SetDisplayType(_unisonEvents[0].PartCount);

            _completeSequence = DOTween.Sequence()
                .Append(transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutSine))
                .Append(transform.DOScale(1f, 0.2f).SetEase(Ease.OutSine))
                .Pause().SetLink(gameObject).SetAutoKill(false);

            foreach (var engineContainer in GameManager.EngineManager.Engines)
            {
                if (engineContainer.UnisonPhrases.Count == 0)
                {
                    continue;
                }
                InitializeIcon(engineContainer);
                SubscribeToEngineEvents(engineContainer);
            }

            _instrumentIconPrefab.gameObject.SetActive(false);
        }

        private void BuildTransitionTimings(double songSpeedMultiplier)
        {
            _transitionTimes = new TransitionTiming[_unisonEvents.Count * 2];
            // Transitions in need to start 0.2 * songSpeedMultiplier seconds before the start of a unison phrase,
            // and transitions out need to start 1 * songSpeedMultiplier seconds after the end of a unison phrase.
            // The ends should always be 0.2 * songSpeedMultiplier seconds ahead of that, unless there is another unison phrase in less than 0.4 seconds,
            // in which case the transition out of the previous, and in of the next, should split whatever time is between them.
            int i = 0;
            while (i < _transitionTimes.Length)
            {
                var unisonEventIndex = i / 2;
                var unisonEvent = _unisonEvents[unisonEventIndex];
                if (i == 0)
                {
                    var startTime = Math.Max(0,
                        unisonEvent.Time - (DISPLAY_PRE_TIME + TRANSITION_DURATION) * songSpeedMultiplier);
                    var endTime = Math.Max(0, unisonEvent.Time - DISPLAY_PRE_TIME * songSpeedMultiplier);
                    _transitionTimes[i] = new TransitionTiming(startTime,
                        endTime);
                    i++;
                }
                else if (i == _transitionTimes.Length - 1)
                {
                    var endTime = Math.Min(GameManager.SongLength,
                        unisonEvent.TimeEnd + (DISPLAY_HOLD_TIME + TRANSITION_DURATION) * songSpeedMultiplier);
                    _transitionTimes[i] =
                        new TransitionTiming(endTime - TRANSITION_DURATION * songSpeedMultiplier, endTime);
                    i++;
                }
                else
                {
                    // This must be an out transition, so we can use it to check the next
                    var nextUnison = _unisonEvents[unisonEventIndex + 1];
                    if (nextUnison.Time - unisonEvent.TimeEnd <
                        (2 * TRANSITION_DURATION + DISPLAY_HOLD_TIME + DISPLAY_PRE_TIME) * songSpeedMultiplier)
                    {
                        var totalTime = nextUnison.Time - unisonEvent.TimeEnd;
                        var holdTime = totalTime * 0.25;
                        var preTime = totalTime * 0.25;
                        var transitionTime = totalTime * 0.25;
                        _transitionTimes[i] = new TransitionTiming(unisonEvent.TimeEnd + holdTime,
                            unisonEvent.TimeEnd + holdTime + transitionTime);
                        _transitionTimes[i + 1] = new TransitionTiming(nextUnison.Time - transitionTime - preTime,
                            nextUnison.Time - preTime);
                    }
                    else
                    {
                        _transitionTimes[i] = new TransitionTiming(
                            unisonEvent.TimeEnd + DISPLAY_HOLD_TIME * songSpeedMultiplier, unisonEvent.TimeEnd +
                            (TRANSITION_DURATION + DISPLAY_HOLD_TIME) * songSpeedMultiplier);
                        _transitionTimes[i + 1] =
                            new TransitionTiming(
                                nextUnison.Time - (TRANSITION_DURATION + DISPLAY_PRE_TIME) * songSpeedMultiplier,
                                nextUnison.Time - (DISPLAY_PRE_TIME * songSpeedMultiplier));
                    }

                    if (i < _transitionTimes.Length - 1)
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        public void SetSongTime(double time)
        {
            // Used when seeking in replays - fix up indexes
            _transitionIndex = 0;
            _unisonEventIndex = 0;
            while (_transitionIndex < _transitionTimes.Length && time > _transitionTimes[_transitionIndex].TimeEnd)
            {
                _transitionIndex++;
            }

            while (_unisonEventIndex < _unisonEvents.Count && time > _unisonEvents[_unisonEventIndex].Time)
            {
                _unisonEventIndex++;
            }
            // The unison events themselves should be fixed up by EngineManager.
        }

        private void InitializeIcon(EngineManager.EngineContainer engineContainer)
        {
            var icon = Instantiate(_instrumentIconPrefab, _iconContainer.transform, false);
            icon.gameObject.SetActive(_unisonEvents[0].ParticipantIds.Contains(engineContainer.EngineId));
            icon.SetIcon(engineContainer.GetInstrumentSprite());
            _instrumentIcons[engineContainer.EngineId] = icon;
        }

        private void SubscribeToEngineEvents(EngineManager.EngineContainer engineContainer)
        {
            if (engineContainer.Engine is GuitarEngine
                guitarEngine)
            {
                guitarEngine.OnNoteHit += (_, note) =>
                {
                    if (!note.IsStarPower)
                    {
                        return;
                    }

                    OnNoteHit(engineContainer.EngineId, note);
                };
                guitarEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                {
                    OnStarPowerPhraseStart(note, noteCount, engineContainer.EngineId);
                };
                guitarEngine.OnStarPowerPhraseMissed += (note) =>
                {
                    OnStarPowerPhraseMissed(note, engineContainer.EngineId);
                };
            }
            else if (engineContainer.Engine is DrumsEngine
                drumEngine)
            {
                drumEngine.OnNoteHit += (_, note) =>
                {
                    if (!note.IsStarPower)
                    {
                        return;
                    }

                    OnNoteHit(engineContainer.EngineId, note);
                };
                drumEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                {
                    OnStarPowerPhraseStart(note, noteCount, engineContainer.EngineId);
                };
                drumEngine.OnStarPowerPhraseMissed += (note) =>
                {
                    OnStarPowerPhraseMissed(note, engineContainer.EngineId);
                };
            }
            else if (engineContainer.Engine is KeysEngine<ProKeysNote>
                proKeysEngine)
            {
                proKeysEngine.OnNoteHit += (_, note) =>
                {
                    if (!note.IsStarPower)
                    {
                        return;
                    }

                    OnNoteHit(engineContainer.EngineId, note);
                };
                proKeysEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                {
                    OnStarPowerPhraseStart(note, noteCount, engineContainer.EngineId);
                };
                proKeysEngine.OnStarPowerPhraseMissed += (note) =>
                {
                    OnStarPowerPhraseMissed(note, engineContainer.EngineId);
                };
            }
            else if (engineContainer.Engine is KeysEngine<GuitarNote>
                fiveLaneKeysEngine)
            {
                fiveLaneKeysEngine.OnNoteHit += (_, note) =>
                {
                    if (!note.IsStarPower)
                    {
                        return;
                    }

                    OnNoteHit(engineContainer.EngineId, note);
                };
                fiveLaneKeysEngine.OnStarPowerPhraseStart += (note, noteCount) =>
                {
                    OnStarPowerPhraseStart(note, noteCount, engineContainer.EngineId);
                };
                fiveLaneKeysEngine.OnStarPowerPhraseMissed += (note) =>
                {
                    OnStarPowerPhraseMissed(note, engineContainer.EngineId);
                };
            }
        }

        private void OnStarPowerPhraseStart(ChartEvent note, int noteCount, int engineId)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (!currentPhrase.ParticipantIds.Contains(engineId))
            {
                return;
            }

            _engineIdToNotesInUnisonPhrase[engineId] = noteCount;
            _engineIdToNoteHitInUnisonPhrase[engineId] = 0;

            var icon = _instrumentIcons[engineId];
            icon.SetProgress(0f);
            if (!icon.gameObject.activeSelf)
            {
                icon.gameObject.SetActive(true);
            }
        }

        private void OnStarPowerPhraseMissed(ChartEvent note, int engineId)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (!currentPhrase.ParticipantIds.Contains(engineId))
            {
                return;
            }

            if (note.Time >= currentPhrase.Time && note.Time <= currentPhrase.TimeEnd)
            {
                // Note is within the current unison phrase
                _headerText.color = Color.red;
                _instrumentIcons[engineId].SetProgress(-1f);
            }
        }

        private void OnNoteHit(int engineId, ChartEvent note)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (note.Time < currentPhrase.Time ||
                note.Time > currentPhrase.TimeEnd ||
                !currentPhrase.ParticipantIds.Contains(engineId) ||
                !_engineIdToNoteHitInUnisonPhrase.TryGetValue(engineId, out var notesHit) ||
                !_engineIdToNotesInUnisonPhrase.TryGetValue(engineId, out var notesInPhrase) ||
                notesHit >= notesInPhrase)
            {
                return;
            }

            _engineIdToNoteHitInUnisonPhrase[engineId]++;

            SetProgress(engineId);
        }

        public void OnUnisonPhraseSuccess()
        {
            _headerText.color = Color.gold;
            _completeSequence.Restart();
        }

        private void UpdateScale()
        {
            if (_transitionIndex >= _transitionTimes.Length)
            {
                return;
            }

            var currentTransition = _transitionTimes[_transitionIndex];
            if (GameManager.VisualTime < currentTransition.Time)
            {
                return;
            }

            if (!_parent.activeSelf)
            {
                _parent.SetActive(true);
            }

            bool isIn = _transitionIndex % 2 == 0;

            var progress = currentTransition.Progress(GameManager.VisualTime);
            var scale = DOVirtual.EasedValue(isIn ? 0f : 1f, isIn ? 1f : 0f, progress, Ease.OutSine);
            _parent.transform.localScale = new Vector3(scale, scale, 1f);
            if (GameManager.VisualTime > currentTransition.TimeEnd && !isIn)
            {
                if (_parent.activeSelf)
                {
                    _parent.SetActive(false);
                }
            }
        }

        private void Update()
        {
            if (_unisonEventIndex >= _unisonEvents.Count || _transitionIndex >= _transitionTimes.Length)
            {
                gameObject.SetActive(false);
                return;
            }

            var currentTransition = _transitionTimes[_transitionIndex];

            UpdateScale();

            if (GameManager.VisualTime > currentTransition.TimeEnd)
            {
                // Move to the next unison phrase
                _transitionIndex++;
                if (_transitionIndex / 2 <= _unisonEventIndex)
                {
                    return;
                }

                if (_transitionIndex % 2 == 0)
                {
                    _unisonEventIndex++;
                }

                if (_unisonEventIndex >= _unisonEvents.Count)
                {
                    gameObject.SetActive(false);
                    return;
                }

                _headerText.color = Color.white;

                int participantCount = _unisonEvents[_unisonEventIndex].PartCount;
                SetDisplayType(participantCount);

                foreach (var engineIds in _engineIdToNotesInUnisonPhrase.Keys)
                {
                    _engineIdToNoteHitInUnisonPhrase[engineIds] = 0;
                    if (participantCount <= 8)
                    {
                        var icon = _instrumentIcons[engineIds];
                        icon.SetProgress(0f);
                        icon.gameObject.SetActive(false);
                        if (_unisonEvents[_unisonEventIndex].ParticipantIds.Contains(engineIds))
                        {
                            if (!icon.gameObject.activeSelf)
                            {
                                icon.gameObject.SetActive(true);
                            }
                        }
                        else if (icon.gameObject.activeSelf)
                        {
                            icon.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        private void SetDisplayType(int participantCount)
        {
            if (participantCount > 8)
            {
                _iconContainer.SetActive(false);
                _unisonBar.gameObject.SetActive(true);
                _unisonBar.SetProgress(0f);
                _unisonBar.SetUnisonInfo(0, participantCount);
            }
            else
            {
                _iconContainer.SetActive(true);
                _unisonBar.gameObject.SetActive(false);
            }
        }

        private void SetProgress(int engineId)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentEvent = _unisonEvents[_unisonEventIndex];
            if (!currentEvent.ParticipantIds.Contains(engineId))
            {
                return;
            }

            if (currentEvent.PartCount > 8)
            {
                var notesHitInUnison = _engineIdToNoteHitInUnisonPhrase.Values.Sum();
                var notesInUnison = _engineIdToNotesInUnisonPhrase.Values.Sum();
                var overallProgress = (float) notesHitInUnison / notesInUnison;
                _unisonBar.SetUnisonInfo(currentEvent.SuccessCount, currentEvent.PartCount);
                _unisonBar.SetProgress(overallProgress);
            }
            else
            {
                _instrumentIcons[engineId].SetProgress((float) _engineIdToNoteHitInUnisonPhrase[engineId] /
                    _engineIdToNotesInUnisonPhrase[engineId]);
            }
        }

        public void OnEditModeChanged()
        {
            _isEditMode = !_isEditMode;
            if (_isEditMode)
            {
                SetDisplayType(1);
                var icon = _instrumentIcons.Values.ElementAt(0);
                icon.gameObject.SetActive(true);
                icon.SetProgress(0f);
                _parent.SetActive(true);
                _parent.transform.localScale = Vector3.one;
            }
            else
            {
                _parent.SetActive(false);
                _parent.transform.localScale = Vector3.zero;
            }
        }
    }
}