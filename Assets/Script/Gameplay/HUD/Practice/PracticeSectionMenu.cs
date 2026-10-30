using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class PracticeSectionMenu : GameplayBehaviour
    {
        protected override bool DisableUntilSongStarts => false;

        private const int SECTION_VIEW_EXTRA = 10;
        private const float SCROLL_TIME = 1f / 60f;

        private PauseMenuManager _pauseMenuManager;

        private bool _navigationPushed;
        private bool _hasSelectedSections;
        private bool _allowWrapAround;

        private List<Section> _sections;
        public IReadOnlyList<Section> Sections => _sections;

        [SerializeField]
        private Transform _sectionContainer;

        [Space]
        [SerializeField]
        private GameObject _sectionViewPrefab;

        private readonly List<PracticeSectionView> _sectionViews = new();

        private readonly PreviewPlayer _previewPlayer = new();

        private int _hoveredIndex;
        public int HoveredIndex
        {
            get => _hoveredIndex;
            private set
            {
                if (_allowWrapAround)
                {
                    if (value > _sections.Count - 1)
                    {
                        _hoveredIndex = 0;
                    }
                    else if (value < 0)
                    {
                        _hoveredIndex = _sections.Count - 1;
                    }
                    else
                    {
                        _hoveredIndex = value;
                    }
                }
                else
                {
                     _hoveredIndex = Mathf.Clamp(value, 0, _sections.Count - 1);
                }

                UpdateSectionViews();
                UpdatePreviewAudio();
            }
        }

        public int? FirstSelectedIndex { get; private set; }
        public int? LastSelectedIndex  { get; private set; }

        private float _scrollTimer;

        private uint _finalTick;
        private double _finalChartTime;

        protected override void GameplayAwake()
        {
            _pauseMenuManager = FindAnyObjectByType<PauseMenuManager>();

            if (!GameManager.IsPractice)
            {
                Destroy(gameObject);
                return;
            }

            GameManager.ChartLoaded += OnChartLoaded;

            // Create all of the section views
            for (int i = 0; i < SECTION_VIEW_EXTRA * 2 + 1; i++)
            {
                int relativeIndex = i - SECTION_VIEW_EXTRA;
                var gameObject = Instantiate(_sectionViewPrefab, _sectionContainer);

                var sectionView = gameObject.GetComponent<PracticeSectionView>();
                sectionView.Init(relativeIndex, this);
                _sectionViews.Add(sectionView);
            }
        }

        private void OnEnable()
        {
            // Wait until the chart has been loaded
            if (_sections is null) return;

            Initialize();
        }

        private void OnDisable()
        {
            if (_navigationPushed)
            {
                Navigator.Instance.PopScheme();
                _navigationPushed = false;
            }

            _ = _previewPlayer.StopPreview();
        }

        protected override void GameplayDestroy()
        {
            _previewPlayer.Dispose();
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _sections = chart.Sections;
            _finalTick = chart.GetLastTick();
            _finalChartTime = chart.SyncTrack.TickToTime(_finalTick);

            //_pauseMenuManager.PushMenu(PauseMenuManager.Menu.SelectSections);
        }

        private void Initialize()
        {
            FirstSelectedIndex = null;
            LastSelectedIndex = null;
            RegisterNavigationScheme();
            UpdateSectionViews();
            UpdatePreviewAudio();
        }

        private void UpdateSectionViews()
        {
            foreach (var sectionView in _sectionViews)
            {
                sectionView.UpdateView();
            }
        }

        private void RegisterNavigationScheme()
        {
            if (_navigationPushed) return;

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", Confirm),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up", ctx => {
                    _allowWrapAround = !ctx.IsRepeat && SettingsManager.Settings.WrapAroundNavigation.Value;
                    HoveredIndex--;
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down", ctx => {
                    _allowWrapAround = !ctx.IsRepeat && SettingsManager.Settings.WrapAroundNavigation.Value;
                    HoveredIndex++;
                })
            }, false));


            _navigationPushed = true;
        }

        private void UpdatePreviewAudio()
        {
            const double FADE_DURATION = 1.25;
            const double PREVIEW_SCROLL_DELAY = .6f;
            float previewVolume = SettingsManager.Settings.PreviewVolume.Value;

            if (previewVolume == 0)
            {
                return;
            }

            var section = _sections[HoveredIndex];
            var startTime = section.Time;
            var length = section.TimeLength;

            _ = _previewPlayer.StopPreview();
            _ = _previewPlayer.StartPreview(
                GameManager.Song,
                previewVolume,
                GlobalVariables.State.SongSpeed,
                PREVIEW_SCROLL_DELAY,
                FADE_DURATION,
                SettingsManager.Settings.CensorMatureContent.Value,
                startTime,
                length);
        }

        private void Confirm()
        {
            if (FirstSelectedIndex == null)
            {
                FirstSelectedIndex = HoveredIndex;
            }
            else
            {
                LastSelectedIndex = HoveredIndex;

                int first = FirstSelectedIndex.Value;
                int last = LastSelectedIndex.Value;

                GameManager.PracticeManager.SetPracticeSection(_sections[first], _sections[last]);
                GameManager.Resume();

                _hasSelectedSections = true;
            }
        }

        private void Back()
        {
            // No sections have been played yet so we must exit
            if (!_hasSelectedSections && FirstSelectedIndex == null)
            {
                GameManager.ForceQuitSong();
            }
            else
            {
                // Unselect starting section
                if(FirstSelectedIndex != null)
                {
                    FirstSelectedIndex = null;
                    UpdateSectionViews();
                }
                else
                {
                    // Go back to practice pause menu
                    _pauseMenuManager.PopMenu();
                    _pauseMenuManager.PushMenu(PauseMenuManager.Menu.PracticePause);
                }
            }
        }

        private void Update()
        {
            if (_scrollTimer > 0f)
            {
                _scrollTimer -= Time.unscaledDeltaTime;
                return;
            }

            var scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            var delta = scroll * Time.unscaledDeltaTime;

            if (delta > 0f)
            {
                HoveredIndex--;
                _scrollTimer = SCROLL_TIME;
            }
            else if (delta < 0f)
            {
                HoveredIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }
    }
}