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

        [Header("References")]
        [SerializeField]
        private PracticeSectionMenu practiceSectionMenu;

        [SerializeField]
        private PracticeHud practiceHud;

        [SerializeField]
        private GameObject scoreDisplayObject;

        private GameManager _gameManager;

        private SongChart _chart;

        private uint _tickStart;
        private uint _tickEnd;

        private uint _lastTick;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
            _gameManager.ChartLoaded += OnChartLoaded;

            Navigator.Instance.NavigationEvent += OnNavigationEvent;
        }

        private void Start()
        {
            if (_gameManager.IsPractice)
            {
                practiceHud.gameObject.SetActive(true);
                scoreDisplayObject.SetActive(false);
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

        private void OnChartLoaded(SongChart chart)
        {
            _chart = chart;
            _lastTick = chart.GetLastTick();
        }

        private void OnNavigationEvent(NavigationContext ctx)
        {
            if (ctx.Action == MenuAction.Left)
            {
                _gameManager.SelectedSongSpeed -= 0.05f;
                GlobalVariables.AudioManager.SetSpeed(_gameManager.SelectedSongSpeed);
            } else if (ctx.Action == MenuAction.Right)
            {
                _gameManager.SelectedSongSpeed += 0.05f;
                GlobalVariables.AudioManager.SetSpeed(_gameManager.SelectedSongSpeed);
            }
        }

        public void DisplayPracticeMenu()
        {
            practiceSectionMenu.gameObject.SetActive(true);
        }

        public void SetPracticeSection(Section start, Section end)
        {
            SetPracticeSection(start.Tick, end.TickEnd);
        }

        public void SetPracticeSection(uint tickStart, uint tickEnd)
        {
            _tickStart = tickStart;
            _tickEnd = tickEnd;

            foreach (var player in _gameManager.Players)
            {
                player.SetPracticeSection(tickStart, tickEnd);
            }

            double songTime = _chart.SyncTrack.TickToTime(tickStart);

            _gameManager.SetSongTime(songTime);
            _gameManager.SetPaused(false, false);

            practiceHud.SetSections(GetSectionsInPractice(tickStart, tickEnd));
        }

        public void AdjustPracticeStartEnd(int start, int end)
        {
            if(_tickStart - start < 0)
            {
                _tickStart = 0;
            }
            else
            {
                _tickStart -= (uint)start;
            }

            if(_tickEnd + end > _lastTick)
            {
                _tickEnd = _lastTick;
            }
            else
            {
                _tickEnd += (uint)end;
            }

            SetPracticeSection(_tickStart, _tickEnd);
        }

        public void ResetPractice()
        {
            foreach (var player in _gameManager.Players)
            {
                player.ResetPracticeSection();
            }

            _gameManager.SetSongTime(_chart.SyncTrack.TickToTime(_tickStart));

            practiceHud.ResetPractice();
        }

        private Section[] GetSectionsInPractice(uint start, uint end)
        {
            return _chart.Sections.Where(s => s.Tick >= start && s.TickEnd <= end).ToArray();
        }
    }
}