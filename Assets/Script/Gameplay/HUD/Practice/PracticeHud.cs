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

        private void Update()
        {

        }

        public void UpdateHud()
        {
            speedPercentText.text = $"{_gameManager.SelectedSongSpeed * 100f:0}%";
        }

        public void SetSections(Section[] sections)
        {
            _sections = sections;
            _currentSectionIndex = 0;
        }
    }
}