using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class PracticeSectionMenu : MonoBehaviour
    {
        private const int SECTION_VIEW_EXTRA = 10;
        private const float SCROLL_TIME = 1f / 60f;

        private GameManager _gameManager;

        private List<Section> _sections;
        public IReadOnlyList<Section> Sections => _sections;

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


        public int? FirstSelectedIndex { get; private set; }
        public int? LastSelectedIndex  { get; private set; }

        private bool _selectedFirstIndex;

        private float _scrollTimer;

        private uint _finalTick;
        private double _finalChartTime;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
        }

        private void Start()
        {
            // Create all of the section views
            for (int i = 0; i < SECTION_VIEW_EXTRA * 2 + 1; i++)
            {
                int relativeIndex = i - SECTION_VIEW_EXTRA;
                var gameObject = Instantiate(_sectionViewPrefab, _sectionContainer);

                // Add
                var sectionView = gameObject.GetComponent<PracticeSectionView>();
                sectionView.Init(relativeIndex, this);
                _sectionViews.Add(sectionView);
            }
        }

        private void OnEnable()
        {
            _gameManager.ChartLoaded += OnChartLoaded;

            if (_sections is not null)
            {
                UpdateSectionViews();
            }

            FirstSelectedIndex = null;

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Confirm", () =>
                {
                    if (!_selectedFirstIndex)
                    {
                        _selectedFirstIndex = true;
                        FirstSelectedIndex = HoveredIndex;
                    }
                    else
                    {
                        LastSelectedIndex = HoveredIndex;

                        int first = FirstSelectedIndex!.Value;
                        int last = LastSelectedIndex!.Value;

                        if (last < first)
                        {
                            (first, last) = (last, first);
                            last++;
                        }

                        if (last >= _sections.Count)
                        {
                            // Not ideal. Need a better way of handling practice sections to display them in the UI
                            _gameManager.SetPracticeSection(_sections[first], new Section("End", _finalChartTime, _finalTick));
                        }
                        else
                        {
                            _gameManager.SetPracticeSection(_sections[first], _sections[last]);
                        }

                        _gameManager.SetPaused(false);
                    }
                }),
                new NavigationScheme.Entry(MenuAction.Up, "Up", () =>
                {
                    HoveredIndex--;
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Up", () =>
                {
                    HoveredIndex++;
                })
            }, false));
        }

        private void OnDisable()
        {
            _gameManager.ChartLoaded -= OnChartLoaded;
            Navigator.Instance.PopScheme();
        }

        private void OnChartLoaded(SongChart chart)
        {
            _sections = chart.Sections;
            _finalTick = chart.GetLastTick();
            _finalChartTime = chart.SyncTrack.TickToTime(_finalTick);

            UpdateSectionViews();
        }

        private void UpdateSectionViews()
        {
            foreach (var sectionView in _sectionViews)
            {
                sectionView.UpdateView();
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