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

namespace YARG.Gameplay.HUD
{
    public class UnisonDisplay : GameplayBehaviour
    {
        private class EngineUnisonInfo
        {
            public readonly List<int> NoteCountInPhrase;
            public          int       UnisonPhraseNotesHit;

            public EngineUnisonInfo(EngineManager.EngineContainer engineContainer,
                List<EngineManager.UnisonEvent> unisonPhrases)
            {
                NoteCountInPhrase = new List<int>();
                UnisonPhraseNotesHit = 0;
                //TODO: This is kinda a bad way to do this, should probably get the chart data directly instead of doing this
                foreach (var phrase in unisonPhrases)
                {
                    if (engineContainer.Engine is BaseEngine<GuitarNote, GuitarEngineParameters, GuitarStats>
                        guitarEngine)
                    {
                        NoteCountInPhrase.Add(guitarEngine.GetNoteCountInTickWindow(phrase.Tick, phrase.TickEnd));
                    }
                    else if (engineContainer.Engine is BaseEngine<DrumNote, DrumsEngineParameters, DrumsStats>
                        drumEngine)
                    {
                        NoteCountInPhrase.Add(drumEngine.GetNoteCountInTickWindow(phrase.Tick, phrase.TickEnd));
                    }
                    else if (engineContainer.Engine is BaseEngine<ProKeysNote, KeysEngineParameters, KeysStats>
                        proKeysEngine)
                    {
                        NoteCountInPhrase.Add(proKeysEngine.GetNoteCountInTickWindow(phrase.Tick, phrase.TickEnd));
                    }
                    else if (engineContainer.Engine is BaseEngine<GuitarNote, KeysEngineParameters, KeysStats>
                        fiveLaneKeysEngine)
                    {
                        NoteCountInPhrase.Add(fiveLaneKeysEngine.GetNoteCountInTickWindow(phrase.Tick, phrase.TickEnd));
                    }
                }
            }
        }
        [SerializeField]
        private GameObject _parent;
        [SerializeField]
        private GameObject _iconContainer;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private UnisonIcon _instrumentIconPrefab;

        private Sequence _showHudSequence;
        private Sequence _hideHudSequence;
        private Sequence _completeSequence;

        private readonly Dictionary<int, UnisonIcon> _instrumentIcons = new();

        private const float BONUS_DISPLAY_TIME = 2;

        private readonly Dictionary<int, EngineUnisonInfo> _engineIdToUnisonInfo = new();
        private readonly List<EngineManager.UnisonEvent>   _unisonEvents            = new();
        private          int                               _unisonEventIndex;

        protected override void OnSongStarted()
        {
            if (GameManager.EngineManager.Engines.Count(e =>
                e.Instrument is not Instrument.Vocals and not Instrument.Harmony) < 2)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (var unisonEvent in GameManager.EngineManager.UnisonEvents)
            {
                if (unisonEvent.ParticipantIds.Count > 1)
                {
                    _unisonEvents.Add(unisonEvent);
                }
            }

            if (_unisonEvents.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _completeSequence = DOTween.Sequence()
                .Append(transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutSine))
                .Append(transform.DOScale(1f, 0.2f).SetEase(Ease.OutSine))
                .Pause().SetLink(gameObject).SetAutoKill(false);
            _showHudSequence = DOTween.Sequence()
                .AppendCallback(() => _parent.SetActive(true))
                .Append(_parent.transform.DOScale(1f, 0.2f).SetEase(Ease.OutSine))
                .Pause().SetLink(_parent).SetAutoKill(false);
            _hideHudSequence = DOTween.Sequence()
                .Append(_parent.transform.DOScale(0f, 0.2f).SetEase(Ease.InSine))
                .AppendCallback(() => _parent.SetActive(false))
                .Pause().SetLink(_parent).SetAutoKill(false);

            foreach (var engineContainer in GameManager.EngineManager.Engines)
            {
                if (engineContainer.UnisonPhrases.Count == 0)
                {
                    continue;
                }
                InitializeIcon(engineContainer);
                _engineIdToUnisonInfo[engineContainer.EngineId] = new EngineUnisonInfo(engineContainer, _unisonEvents);
                SubscribeToEngineEvents(engineContainer);
            }

            _instrumentIconPrefab.gameObject.SetActive(false);
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
                guitarEngine.OnNoteMissed += (_, note) => { OnNoteMiss(engineContainer.EngineId, note); };
                guitarEngine.OnOverstrum += () => { OnOverstrum(engineContainer.EngineId); };
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
                drumEngine.OnNoteMissed += (_, note) => { OnNoteMiss(engineContainer.EngineId, note); };
                drumEngine.OnOverhit += () => { OnOverstrum(engineContainer.EngineId); };
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
                proKeysEngine.OnNoteMissed += (_, note) => { OnNoteMiss(engineContainer.EngineId, note); };
                proKeysEngine.OnOverhit += (key) => { OnOverstrum(engineContainer.EngineId); };
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
                fiveLaneKeysEngine.OnNoteMissed += (_, note) => { OnNoteMiss(engineContainer.EngineId, note); };
                fiveLaneKeysEngine.OnOverhit += (key) => { OnOverstrum(engineContainer.EngineId); };
            }
        }

        private void OnNoteHit(int engineId, ChartEvent note)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (!currentPhrase.ParticipantIds.Contains(engineId) ||
                !_engineIdToUnisonInfo.TryGetValue(engineId, out var unison) ||
                unison.NoteCountInPhrase[_unisonEventIndex] == unison.UnisonPhraseNotesHit)
            {
                return;
            }

            if (note.Time >= currentPhrase.Time && note.Time <= currentPhrase.TimeEnd)
            {
                unison.UnisonPhraseNotesHit++;

                _instrumentIcons[engineId]
                    .SetProgress((float) unison.UnisonPhraseNotesHit / unison.NoteCountInPhrase[_unisonEventIndex]);
                if (currentPhrase.Awarded)
                {
                    _completeSequence.Restart();
                    _headerText.color = Color.gold;
                }
            }
        }

        private void OnNoteMiss(int engineId, ChartEvent note)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (!currentPhrase.ParticipantIds.Contains(engineId) ||
                !_engineIdToUnisonInfo.TryGetValue(engineId, out var unison) ||
                unison.NoteCountInPhrase[_unisonEventIndex] == unison.UnisonPhraseNotesHit)
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

        private void OnOverstrum(int engineId)
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];

            if (!currentPhrase.ParticipantIds.Contains(engineId) ||
                !_engineIdToUnisonInfo.TryGetValue(engineId, out var unison) ||
                unison.NoteCountInPhrase[_unisonEventIndex] == unison.UnisonPhraseNotesHit)
            {
                return;
            }

            if (GameManager.SongTime >= currentPhrase.Time && GameManager.SongTime <= currentPhrase.TimeEnd)
            {
                unison.UnisonPhraseNotesHit = 0;
            }
        }

        private void Update()
        {
            if (_unisonEventIndex >= _unisonEvents.Count)
            {
                gameObject.SetActive(false);
                return;
            }

            var currentPhrase = _unisonEvents[_unisonEventIndex];
            if (GameManager.SongTime > currentPhrase.Time - _showHudSequence.Duration() &&
                GameManager.SongTime < currentPhrase.TimeEnd + BONUS_DISPLAY_TIME)
            {
                if (!_parent.activeSelf && !_showHudSequence.IsPlaying())
                {
                    _showHudSequence.Restart();
                }
            }
            else
            {
                if (_parent.activeSelf && !_hideHudSequence.IsPlaying())
                {
                    _hideHudSequence.Restart();
                }
            }

            if (GameManager.SongTime > currentPhrase.TimeEnd + BONUS_DISPLAY_TIME + _hideHudSequence.Duration())
            {
                // Move to the next unison phrase
                _unisonEventIndex++;
                if (_unisonEventIndex >= _unisonEvents.Count)
                {
                    gameObject.SetActive(false);
                    return;
                }

                foreach (var engineInfo in _engineIdToUnisonInfo)
                {
                    engineInfo.Value.UnisonPhraseNotesHit = 0;
                    var icon = _instrumentIcons[engineInfo.Key];
                    icon.SetProgress(0f);
                    icon.gameObject.SetActive(false);
                    if (_unisonEvents[_unisonEventIndex].ParticipantIds.Contains(engineInfo.Key))
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

                _headerText.color = Color.white;
            }
        }
    }
}