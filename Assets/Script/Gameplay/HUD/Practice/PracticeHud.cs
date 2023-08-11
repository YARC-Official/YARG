using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class PracticeHud : MonoBehaviour
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

        private GameManager _gameManager;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _sections = Array.Empty<Section>();
            _currentSectionIndex = 0;
        }

        private void Start()
        {
            if (!_gameManager.IsPractice)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_gameManager.Players is null)
            {
                return;
            }

            speedPercentText.text = $"{_gameManager.SelectedSongSpeed * 100f:0}%";

            int notesHit = 0;
            int totalNotes = 0;
            foreach (var player in _gameManager.Players)
            {
                notesHit += player.NotesHit;
                totalNotes += player.TotalNotes;
            }

            _percentHit = (float)notesHit / totalNotes;

            notesHitTotalText.text = $"{notesHit}/{totalNotes}";
            percentHitText.text = $"{_percentHit * 100f:0}%";

            while(_currentSectionIndex < _sections.Length && _gameManager.SongTime >= _sections[_currentSectionIndex].TimeEnd)
            {
                _currentSectionIndex++;

                if(_currentSectionIndex < _sections.Length)
                {
                    sectionText.text = $"{_sections[_currentSectionIndex].Name}";
                }
            }
        }

        public void ResetPractice()
        {
            if(_percentHit > _bestPercentHit)
            {
                _bestPercentHit = _percentHit;

                bestPercentText.text = $"{_bestPercentHit * 100f:0}%";
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
        }
    }
}