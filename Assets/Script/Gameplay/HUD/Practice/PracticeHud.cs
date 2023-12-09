using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class PracticeHud : GameplayBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI speedPercentText;

        [SerializeField]
        private TextMeshProUGUI sectionText;

        [SerializeField]
        private TextMeshProUGUI percentHitText;

        [SerializeField]
        private TextMeshProUGUI bestPercentText;

        [SerializeField]
        private TextMeshProUGUI notesHitTotalText;

        private float _speed;
        private float _percentHit;
        private float _bestPercentHit;

        private int _notesHit;

        private Section[] _sections;

        private int _currentSectionIndex;

        protected override void GameplayAwake()
        {
            if (!GameManager.IsPractice)
            {
                Destroy(gameObject);
            }

            _sections = Array.Empty<Section>();
            _currentSectionIndex = 0;
        }

        protected override void GameplayUpdate()
        {
            if (GameManager.Players is null)
            {
                return;
            }

            int notesHit = 0;
            int totalNotes = 0;
            foreach (var player in GameManager.Players)
            {
                notesHit += player.NotesHit;
                totalNotes += player.TotalNotes;
            }

            if(totalNotes == 0)
            {
                _percentHit = 0f;
            }
            else
            {
                _percentHit = (float)notesHit / totalNotes;
            }

            notesHitTotalText.text = $"{notesHit}/{totalNotes}";
            percentHitText.text = $"{Mathf.FloorToInt(_percentHit * 100)}%";

            while(_currentSectionIndex < _sections.Length && GameManager.SongTime >= _sections[_currentSectionIndex].TimeEnd)
            {
                _currentSectionIndex++;

                if(_currentSectionIndex < _sections.Length)
                {
                    sectionText.text = $"{_sections[_currentSectionIndex].Name}";
                }
            }
        }

        protected override void SetSpeed(float speed)
        {
            speedPercentText.text = $"{GameManager.SelectedSongSpeed * 100f:0}%";
        }

        public void ResetPractice()
        {
            if(_percentHit > _bestPercentHit)
            {
                _bestPercentHit = _percentHit;

                bestPercentText.text = $"{Mathf.FloorToInt(_percentHit * 100)}%";
            }

            _currentSectionIndex = 0;

            if(_sections.Length > 0)
            {
                sectionText.text = $"{_sections[_currentSectionIndex].Name}";
            }
        }

        public void ResetStats()
        {
            _bestPercentHit = 0f;
            bestPercentText.text = "0%";
        }

        public void SetSections(Section[] sections)
        {
            _sections = sections;
            _currentSectionIndex = 0;

            _percentHit = 0f;
            _bestPercentHit = 0f;

            bestPercentText.text = "0%";
            sectionText.text = $"{_sections[_currentSectionIndex].Name}";
        }
    }
}