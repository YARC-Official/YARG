using System;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class PracticeHud : GameplayBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI _speedPercentText;

        [SerializeField]
        private TextMeshProUGUI _sectionHeaderText;
        [SerializeField]
        private TextMeshProUGUI _sectionText;
        [SerializeField]

        private TextMeshProUGUI _percentHitText;
        [SerializeField]
        private TextMeshProUGUI _bestPercentText;

        [SerializeField]
        private TextMeshProUGUI _notesHitTotalText;

        private float _speed;
        private float _percentHit;
        private float _bestPercentHit;

        private int _notesHit;

        private Section[] _sections;

        private int _currentSectionIndex;

        private bool _speedChanged = false;
        private int  _offsetNotesHit = 0;

        protected override void GameplayAwake()
        {
            _sections = Array.Empty<Section>();
            _currentSectionIndex = 0;
        }

        private void Start()
        {
            if (!GameManager.IsPractice)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (GameManager.Players is null)
            {
                return;
            }

            _speedPercentText.SetTextFormat("{0:0}%", GameManager.SongSpeed * 100f);

            int notesHit = 0;
            int totalNotes = 0;
            foreach (var player in GameManager.Players)
            {
                notesHit += player.NotesHit;
                totalNotes += player.TotalNotes;
            }

            if (_speedChanged)
            {
                notesHit -= _offsetNotesHit;
            }

            if (totalNotes == 0)
            {
                _percentHit = 0f;
            }
            else
            {
                _percentHit = (float)notesHit / totalNotes;
            }

            _notesHitTotalText.SetTextFormat("{0}/{1}", notesHit, totalNotes);
            _percentHitText.SetTextFormat("{0}%", Mathf.FloorToInt(_percentHit * 100));

            while(_currentSectionIndex < _sections.Length && GameManager.SongTime >= _sections[_currentSectionIndex].TimeEnd)
            {
                _currentSectionIndex++;

                if(_currentSectionIndex < _sections.Length)
                {
                    _sectionText.text = _sections[_currentSectionIndex].Name;
                }
            }
        }

        public void ResetPractice()
        {
            _speedChanged = false;

            if (_percentHit > _bestPercentHit)
            {
                _bestPercentHit = _percentHit;

                _bestPercentText.SetTextFormat("{0}%", Mathf.FloorToInt(_percentHit * 100));
            }

            _currentSectionIndex = 0;

            if (_sections.Length > 0)
            {
                _sectionText.text = _sections[_currentSectionIndex].Name;
            }
        }

        public void ResetStats()
        {
            if (GameManager.Players != null)
            {
                _offsetNotesHit = 0;
                foreach (var player in GameManager.Players)
                {
                    _offsetNotesHit += player.NotesHit;
                }
            }

            _speedChanged = true;
            _percentHit = 0f;
            _bestPercentHit = 0f;
            _bestPercentText.text = "0%";
        }

        public void SetSections(Section[] sections)
        {
            _sections = sections;
            _currentSectionIndex = 0;

            _percentHit = 0f;
            _bestPercentHit = 0f;
            _offsetNotesHit = 0;
            _speedChanged = false;

            _bestPercentText.text = "0%";
            _sectionText.text = _sections[_currentSectionIndex].Name;
        }
    }
}