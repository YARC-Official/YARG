using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Menu.Navigation;

namespace YARG.Menu.Gameplay
{
    public class PracticeSectionMenu : MonoBehaviour
    {
        private const int SECTION_VIEW_EXTRA = 5;
        private const float SCROLL_TIME = 1f / 60f;

        private GameManager _gameManager;

        private List<Section> Sections;

        [SerializeField]
        private Transform _replayContainer;
        [SerializeField]
        private Scrollbar _scrollbar;

        [Space]
        [SerializeField]
        private GameObject _replayViewPrefab;

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            private set
            {
                // Properly wrap the value
                if (value < 0)
                {
                    _selectedIndex = Sections.Count - 1;
                }
                else if (value >= Sections.Count)
                {
                    _selectedIndex = 0;
                }
                else
                {
                    _selectedIndex = value;
                }

                UpdateScrollbar();
                UpdateSectionViews();
            }
        }

        private readonly List<PracticeView> _sectionViews = new();

        private float _scrollTimer;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
        }

        private void Start()
        {
            _gameManager.ChartLoaded += chart =>
            {
                Sections = chart.Sections;

                // Create all of the section views
                for (int i = 0; i < SECTION_VIEW_EXTRA * 2 + 1; i++)
                {
                    var gameObject = Instantiate(_replayViewPrefab, _replayContainer);

                    // Add
                    var sectionView = gameObject.GetComponent<PracticeView>();
                    _sectionViews.Add(sectionView);
                }
            };
        }

        private void OnEnable()
        {
            UpdateSectionViews();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void UpdateSectionViews()
        {
            for (int i = 0; i < _sectionViews.Count; i++)
            {
                // Hide if it's not in range
                int relativeIndex = i - SECTION_VIEW_EXTRA;
                int realIndex = SelectedIndex + relativeIndex;
                if (realIndex < 0 || realIndex >= Sections.Count)
                {
                    _sectionViews[i].Hide();
                    continue;
                }

                // Otherwise, show as a replay
                _sectionViews[i].ShowAsSection(relativeIndex == 0, Sections[realIndex]);
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
                SelectedIndex--;
                _scrollTimer = SCROLL_TIME;
                return;
            }

            if (delta < 0f)
            {
                SelectedIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }

        public void OnScrollBarChange()
        {
            SelectedIndex = Mathf.FloorToInt(_scrollbar.value * (Sections.Count - 1));
        }

        private void UpdateScrollbar()
        {
            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / Sections.Count);
        }
    }
}