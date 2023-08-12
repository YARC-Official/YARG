﻿using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Menu.Navigation;

namespace YARG.Gameplay
{
    public class PracticeManager : MonoBehaviour
    {
        private const double SECTION_RESTART_DELAY = 2;

        [Header("References")]
        [SerializeField]
        private PauseMenuManager _pauseMenu;
        [SerializeField]
        private PracticeHud _practiceHud;
        [SerializeField]
        private GameObject _scoreDisplayObject;

        private GameManager _gameManager;

        private SongChart _chart;

        private uint _tickStart;
        private uint _tickEnd;
        private double _timeStart;
        private double _timeEnd;

        private uint _lastTick;

        public bool HasSelectedSection { get; private set; }

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            enabled = false;
            _gameManager.ChartLoaded += OnChartLoaded;

            Navigator.Instance.NavigationEvent += OnNavigationEvent;
        }

        private void Start()
        {
            if (_gameManager.IsPractice)
            {
                _practiceHud.gameObject.SetActive(true);
                _scoreDisplayObject.SetActive(false);
            }
            else
            {
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            _gameManager.ChartLoaded -= OnChartLoaded;
            Navigator.Instance.NavigationEvent -= OnNavigationEvent;
        }

        private void Update()
        {
            if (_gameManager.Paused)
                return;

            double endPoint = _timeEnd + (SECTION_RESTART_DELAY * _gameManager.SelectedSongSpeed);
            if (_gameManager.SongTime >= endPoint)
                ResetPractice();
        }

        private void OnChartLoaded(SongChart chart)
        {
            _gameManager.ChartLoaded -= OnChartLoaded;
            enabled = true;

            _chart = chart;
            _lastTick = chart.GetLastTick();
        }

        private void OnNavigationEvent(NavigationContext ctx)
        {
            switch (ctx.Action)
            {
                // Song speed
                case MenuAction.Left:
                    _gameManager.AdjustSongSpeed(-0.05f);
                    _practiceHud.ResetStats();
                    break;
                case MenuAction.Right:
                    _gameManager.AdjustSongSpeed(0.05f);
                    _practiceHud.ResetStats();
                    break;

                // Reset
                case MenuAction.Select:
                    if (_gameManager.Paused)
                    {
                        return;
                    }

                    ResetPractice();
                    break;
            }
        }

        public void DisplayPracticeMenu()
        {
            _gameManager.Pause(showMenu: false);
            _pauseMenu.PushMenu(PauseMenuManager.Menu.SelectSections);
        }

        public void SetPracticeSection(Section start, Section end)
        {
            SetPracticeSection(start.Tick, end.TickEnd, start.Time, end.TimeEnd);
        }

        public void SetPracticeSection(uint tickStart, uint tickEnd, double timeStart, double timeEnd)
        {
            _tickStart = tickStart;
            _tickEnd = tickEnd;
            _timeStart = timeStart;
            _timeEnd = timeEnd;

            foreach (var player in _gameManager.Players)
            {
                player.SetPracticeSection(tickStart, tickEnd);
            }

            _gameManager.SetSongTime(timeStart);
            _gameManager.Resume(inputCompensation: false);

            _practiceHud.SetSections(GetSectionsInPractice(tickStart, tickEnd));
            HasSelectedSection = true;
        }

        public void ResetPractice()
        {
            foreach (var player in _gameManager.Players)
            {
                player.ResetPracticeSection();
            }

            _gameManager.SetSongTime(_timeStart);

            _practiceHud.ResetPractice();
        }

        private Section[] GetSectionsInPractice(uint start, uint end)
        {
            return _chart.Sections.Where(s => s.Tick >= start && s.TickEnd <= end).ToArray();
        }
    }
}