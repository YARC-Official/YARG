using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class PracticeSectionMenu : MonoBehaviour
    {
        private const int SECTION_VIEW_EXTRA = 10;
        private const float SCROLL_TIME = 1f / 60f;

        private GameManager _gameManager;

        private List<Section> _sections;

        [SerializeField]
        private Transform _sectionContainer;
        [SerializeField]
        private Scrollbar _scrollbar;

        [Space]
        [SerializeField]
        private GameObject _sectionViewPrefab;

        private readonly List<PracticeSectionView> _sectionViews = new();

        private int _hoveredIndex;
        public int HoveredIndex
        {
            get => _hoveredIndex;
            private set
            {
                // Properly wrap the value
                if (value < 0)
                {
                    _hoveredIndex = _sections.Count - 1;
                }
                else if (value >= _sections.Count)
                {
                    _hoveredIndex = 0;
                }
                else
                {
                    _hoveredIndex = value;
                }

                UpdateSectionViews();
            }
        }

        private float _scrollTimer;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
        }

        private void Start()
        {
            // Create all of the section views
            for (int i = 0; i < SECTION_VIEW_EXTRA * 2 + 1; i++)
            {
                var gameObject = Instantiate(_sectionViewPrefab, _sectionContainer);

                // Add
                var sectionView = gameObject.GetComponent<PracticeSectionView>();
                _sectionViews.Add(sectionView);
                sectionView.Hide();
            }
        }

        private void OnEnable()
        {
            _gameManager.ChartLoaded += OnChartLoaded;

            if (_sections is not null)
            {
                UpdateSectionViews();
            }
        }

        private void OnDisable()
        {
            _gameManager.ChartLoaded -= OnChartLoaded;
        }

        private void OnChartLoaded(SongChart chart)
        {
            _sections = chart.Sections;

            UpdateSectionViews();
        }

        private void UpdateSectionViews()
        {
            for (int i = 0; i < _sectionViews.Count; i++)
            {
                // Hide if it's not in range
                int relativeIndex = i - SECTION_VIEW_EXTRA;
                int realIndex = HoveredIndex + relativeIndex;
                if (realIndex < 0 || realIndex >= _sections.Count)
                {
                    _sectionViews[i].Hide();
                    continue;
                }

                // Otherwise, show as a replay
                _sectionViews[i].ShowAsSection(_sections[realIndex]);
            }
        }

        private void Update()
        {
            if (_scrollTimer > 0f)
            {
                _scrollTimer -= Time.deltaTime;
                return;
            }

            var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

            if (delta > 0f)
            {
                HoveredIndex--;
                _scrollTimer = SCROLL_TIME;
                return;
            }

            if (delta < 0f)
            {
                HoveredIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }
    }
}