using System.Linq;
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

        public double TimeStart { get; private set; }
        public double TimeEnd   { get; private set; }

        private uint   _sectionStartTick;
        private uint   _sectionEndTick;
        private double _sectionStartTime;
        private double _sectionEndTime;

        private uint _tickStart;
        private uint _tickEnd;

        private uint _lastTick;

        public bool HasSelectedSection    { get; private set; }
        public bool HasUpdatedAbPositions { get; private set; }

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            Navigator.Instance.NavigationEvent += OnNavigationEvent;
        }

        private void Start()
        {
            if (_gameManager.IsPractice)
            {
                _practiceHud.gameObject.SetActive(true);
                _scoreDisplayObject.SetActive(false);

                enabled = false;
                _gameManager.ChartLoaded += OnChartLoaded;
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
            if (_gameManager.Paused) return;

            double endPoint = TimeEnd + (SECTION_RESTART_DELAY * _gameManager.SelectedSongSpeed);
            if (_gameManager.SongTime >= endPoint) ResetPractice();
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
            _sectionStartTick = start.Tick;
            _sectionStartTime = start.Time;

            _sectionEndTick = end.TickEnd;
            _sectionEndTime = end.TimeEnd;

            SetPracticeSection(start.Tick, end.TickEnd, start.Time, end.TimeEnd);
        }

        public void SetPracticeSection(uint tickStart, uint tickEnd, double timeStart, double timeEnd)
        {
            _tickStart = tickStart;
            _tickEnd = tickEnd;
            TimeStart = timeStart;
            TimeEnd = timeEnd;

            foreach (var player in _gameManager.Players)
            {
                player.SetPracticeSection(tickStart, tickEnd);
            }

            _gameManager.SetSongTime(timeStart);
            _gameManager.Resume(inputCompensation: false);

            _practiceHud.SetSections(GetSectionsInPractice(_sectionStartTick, _sectionEndTick));
            HasSelectedSection = true;
        }

        public void SetAPosition(double time)
        {
            // We do this because we want to snap to the exact time of a tick, not in between 2 ticks.
            uint tick = _chart.SyncTrack.TimeToTick(time);
            double startTime = _chart.SyncTrack.TickToTime(tick);

            _tickStart = tick;
            TimeStart = startTime;

            HasUpdatedAbPositions = true;
        }

        public void SetBPosition(double time)
        {
            // We do this because we want to snap to the exact time of a tick, not in between 2 ticks.
            uint tick = _chart.SyncTrack.TimeToTick(time);
            double endTime = _chart.SyncTrack.TickToTime(tick);

            _tickEnd = tick;
            TimeEnd = endTime;

            HasUpdatedAbPositions = true;
        }

        public void ResetAbPositions()
        {
            _tickStart = _sectionStartTick;
            TimeStart = _sectionStartTime;

            _tickEnd = _sectionEndTick;
            TimeEnd = _sectionEndTime;

            HasUpdatedAbPositions = true;
        }

        public void ResetPractice()
        {
            _practiceHud.ResetPractice();

            if (HasUpdatedAbPositions)
            {
                SetPracticeSection(_tickStart, _tickEnd, TimeStart, TimeEnd);
                HasUpdatedAbPositions = false;
                return;
            }

            foreach (var player in _gameManager.Players)
            {
                player.ResetPracticeSection();
            }

            _gameManager.SetSongTime(TimeStart);
        }

        private Section[] GetSectionsInPractice(uint start, uint end)
        {
            return _chart.Sections.Where(s => s.Tick >= start && s.TickEnd <= end).ToArray();
        }
    }
}