using System;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG.Gameplay
{
    public class PracticeManager : GameplayBehaviour
    {
        private const double SECTION_RESTART_DELAY = 2;
        private const double PRACTICE_ENTRY_LOOKBACK = 10; // seconds

        [Header("References")]
        [SerializeField]
        private PauseMenuManager _pauseMenu;
        [SerializeField]
        private PracticeHud _practiceHud;
        [SerializeField]
        private GameObject _scoreDisplayObject;

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

        private void Start()
        {
            if (!GameManager.IsPractice)
            {
                Destroy(this);
                return;
            }

            Navigator.Instance.NavigationEvent += OnNavigationEvent;
            _practiceHud.gameObject.SetActive(true);
            _scoreDisplayObject.SetActive(false);
        }

        protected override void GameplayDestroy()
        {
            Navigator.Instance.NavigationEvent -= OnNavigationEvent;
        }

        private void Update()
        {
            if (GameManager.Paused)
            {
                return;
            }

            // Reset when A/B positions were adjusted (e.g. from the pause menu) so that
            // stats are cleared and the song seeks to the updated start position on resume.
            double endPoint = TimeEnd + (SECTION_RESTART_DELAY * GameManager.SongSpeed);
            if (HasUpdatedAbPositions || GameManager.SongTime >= endPoint)
            {
                ResetPractice();
            }
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _chart = chart;
            _lastTick = chart.GetLastTick();
        }

        private void OnNavigationEvent(NavigationContext ctx)
        {
            if (GameManager.Paused)
            {
                return;
            }

            switch (ctx.Action)
            {
                // Song speed
                case MenuAction.Left:
                    GameManager.AdjustSongSpeed(-0.05f);
                    _practiceHud.ResetStats();
                    break;
                case MenuAction.Right:
                    GameManager.AdjustSongSpeed(0.05f);
                    _practiceHud.ResetStats();
                    break;
                // Reset
                case MenuAction.Select:
                    ResetPractice();
                    break;
            }
        }

        public void DisplayPracticeMenu()
        {
            GameManager.Pause(showMenu: false);

            var savedInputTime = GlobalVariables.State.SavedInputTime;
            if (savedInputTime.HasValue)
            {
                double endTime = savedInputTime.Value;
                double startTime = Math.Max(0.0, endTime - PRACTICE_ENTRY_LOOKBACK);

                var sections = _chart.Sections;
                if (startTime < endTime && sections.Count > 0)
                {
                    var startSection = FindSectionAtTime(startTime);
                    var endSection = FindSectionAtTime(endTime);
                    SetPracticeSection(startSection, endSection);
                    SetAPosition(startTime);
                    SetBPosition(endTime);
                    double restartDelay = SettingsManager.Settings.PracticeRestartDelay.Value;
                    GameManager.SetSongTime(TimeStart, restartDelay);
                    _pauseMenu.PushMenu(PauseMenuManager.Menu.PracticePause);
                    return;
                }
            }

            _pauseMenu.PushMenu(PauseMenuManager.Menu.SelectSections);
        }

        public void SetPracticeSection(Section start, Section end)
        {
            bool includesLastSection = _chart.Sections[^1] == end;

            if (start.Time > end.Time)
            {
                (start, end) = (end, start);
            }

            _sectionStartTick = start.Tick;
            _sectionStartTime = start.Time;

            _sectionEndTick = end.TickEnd;
            _sectionEndTime = end.TimeEnd;

            if (end.Tick == _lastTick || includesLastSection)
            {
                _sectionEndTick += 1;
                _sectionEndTime += 0.01;
            }

            SetPracticeSection(_sectionStartTick, _sectionEndTick, _sectionStartTime, _sectionEndTime);
        }

        public void SetPracticeSection(uint tickStart, uint tickEnd, double timeStart, double timeEnd)
        {
            if (timeStart > timeEnd)
            {
                (timeStart, timeEnd) = (timeEnd, timeStart);
                (tickStart, tickEnd) = (tickEnd, tickStart);
            }

            _tickStart = tickStart;
            _tickEnd = tickEnd;
            TimeStart = timeStart;
            TimeEnd = timeEnd;

            bool allowPracticeSP = SettingsManager.Settings.EnablePracticeSP.Value;
            foreach (var player in GameManager.Players)
            {
                player.SetPracticeSection(tickStart, tickEnd);
                player.BaseEngine.AllowStarPower(allowPracticeSP);
            }

            GameManager.VocalTrack.AllowStarPower = allowPracticeSP;
            GameManager.VocalTrack.SetPracticeSection(tickStart, tickEnd);

            GameManager.SetSongTime(timeStart, SettingsManager.Settings.PracticeRestartDelay.Value);

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

            if(TimeStart > TimeEnd)
            {
                TimeStart = TimeEnd;
                _tickStart = _tickEnd;
            }

            HasUpdatedAbPositions = true;
        }

        public void SetBPosition(double time)
        {
            // We do this because we want to snap to the exact time of a tick, not in between 2 ticks.
            uint tick = _chart.SyncTrack.TimeToTick(time);
            double endTime = _chart.SyncTrack.TickToTime(tick);

            _tickEnd = tick;
            TimeEnd = endTime;

            if(TimeEnd < TimeStart)
            {
                TimeEnd = TimeStart;
                _tickEnd = _tickStart;
            }

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
                GameManager.Resume();
                HasUpdatedAbPositions = false;
                return;
            }

            foreach (var player in GameManager.Players)
            {
                player.ResetPracticeSection();
            }
            GameManager.VocalTrack.ResetPracticeSection();

            GameManager.SetSongTime(TimeStart, SettingsManager.Settings.PracticeRestartDelay.Value);
            GameManager.Resume();
        }

        private Section[] GetSectionsInPractice(uint start, uint end)
        {
            return _chart.Sections.Where(s => s.Tick >= start && s.TickEnd <= end).ToArray();
        }

        private Section FindSectionAtTime(double time)
        {
            var sections = _chart.Sections;
            // Fall back to sections[0] when time precedes the first section —
            // some charts have an unsectioned intro before the first marker.
            return sections[0].Time <= time
                ? sections.Last(s => s.Time <= time)
                : sections[0];
        }
    }
}