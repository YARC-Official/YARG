using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class PracticePause : GenericPause
    {
        [Header("Positioning")]
        [SerializeField] private TextMeshProUGUI _aPositionText;
        [SerializeField] private TextMeshProUGUI _bPositionText;


        private const float SCROLL_TIME = 1f / 60f;

        private const double SCROLL_ADJUST_SECONDS = 5.0;
        private const double NAV_ADJUST_SECONDS = 1.0;

        private const int SCROLL_ADJUST_BARS = 1;
        private const int NAV_ADJUST_BARS = 1;

        private NavigatableBehaviour _aPositionNav;
        private NavigatableBehaviour _bPositionNav;
        private float _scrollTimer;

        private bool IsMeasureMode => SettingsManager.Settings.UseMeasureSnapping.Value;

        protected override void OnEnable()
        {
            // Cache navigatable references for the A/B position buttons
            if (_aPositionNav == null)
            {
                _aPositionNav = _aPositionText.GetComponentInParent<NavigatableBehaviour>();
            }
            if (_bPositionNav == null)
            {
                _bPositionNav = _bPositionText.GetComponentInParent<NavigatableBehaviour>();
            }

            // Use base scheme for standard navigation (Select, Back, Up, Down)
            base.OnEnable();

            // Handle Left/Right via NavigationEvent so they don't appear in the help bar
            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent += OnNavigationEvent;
            }

            UpdatePositionText();
        }

        protected override void OnDisable()
        {
            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            }
            base.OnDisable();
        }

        private void OnNavigationEvent(NavigationContext ctx)
        {
            switch (ctx.Action)
            {
                case MenuAction.Left:
                    HandleAdjustment(-1, false);
                    break;
                case MenuAction.Right:
                    HandleAdjustment(1, false);
                    break;
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
                HandleAdjustment(1, true);
                _scrollTimer = SCROLL_TIME;
            }
            else if (delta < 0f)
            {
                HandleAdjustment(-1, true);
                _scrollTimer = SCROLL_TIME;
            }
        }

        private void HandleAdjustment(int directionMultiplier, bool isScroll)
        {
            var group = NavigationGroup.CurrentNavigationGroup;
            if (group == null || group.SelectedBehaviour == null)
                return;

            var selected = group.SelectedBehaviour;

            if (selected == _aPositionNav || selected == _bPositionNav)
            {
                if (IsMeasureMode)
                {
                    int amount = isScroll ? SCROLL_ADJUST_BARS : NAV_ADJUST_BARS;
                    AdjustBarPosition(selected, amount * directionMultiplier);
                }
                else
                {
                    double amount = isScroll ? SCROLL_ADJUST_SECONDS : NAV_ADJUST_SECONDS;
                    AdjustTimePosition(selected, amount * directionMultiplier);
                }
            }
        }

        private void AdjustTimePosition(NavigatableBehaviour selected, double deltaSeconds)
        {
            if (selected == _aPositionNav)
            {
                double newTime = GameManager.PracticeManager.TimeStart + deltaSeconds;
                // Clamp A so it cannot cross B
                newTime = Math.Min(GameManager.PracticeManager.TimeEnd, Math.Max(0, newTime));
                GameManager.PracticeManager.SetAPosition(newTime);
            }
            else if (selected == _bPositionNav)
            {
                double newTime = GameManager.PracticeManager.TimeEnd + deltaSeconds;
                // Clamp B so it cannot cross A
                newTime = Math.Min(GameManager.SongLength, Math.Max(GameManager.PracticeManager.TimeStart, newTime));
                GameManager.PracticeManager.SetBPosition(newTime);
            }

            UpdatePositionText();
        }

        private void AdjustBarPosition(NavigatableBehaviour selected, int deltaBars)
        {
            int aBar = GetBarAtTime(GameManager.PracticeManager.TimeStart);
            int bBar = GetBarAtTime(GameManager.PracticeManager.TimeEnd);

            if (selected == _aPositionNav)
            {
                int newABar = Math.Max(0, aBar + deltaBars);
                // Clamp A so it cannot cross B
                if (newABar >= bBar)
                {
                    newABar = Math.Max(0, bBar - 1);
                }
                double newATime = Math.Min(GameManager.SongLength, Math.Max(0, GetTimeAtBar(newABar)));
                GameManager.PracticeManager.SetAPosition(newATime);
            }
            else if (selected == _bPositionNav)
            {
                int newBBar = Math.Max(1, bBar + deltaBars); 
                // Clamp B so it cannot cross A
                if (newBBar <= aBar)
                {
                    newBBar = aBar + 1;
                }
                double newBTime = Math.Min(GameManager.SongLength, Math.Max(0, GetTimeAtBar(newBBar)));
                GameManager.PracticeManager.SetBPosition(newBTime);
            }

            UpdatePositionText();
        }

        public void SetAPosition()
        {
            if (IsMeasureMode)
            {
                int currentBar = GetBarAtTime(GameManager.InputTime);
                int bBar = GetBarAtTime(GameManager.PracticeManager.TimeEnd);

                if (currentBar >= bBar)
                {
                    currentBar = Math.Max(0, bBar - 1);
                }
                GameManager.PracticeManager.SetAPosition(GetTimeAtBar(currentBar));
            }
            else
            {
                double newTime = Math.Min(GameManager.PracticeManager.TimeEnd, GameManager.InputTime);
                GameManager.PracticeManager.SetAPosition(newTime);
            }
            
            UpdatePositionText();
        }

        public void SetBPosition()
        {
            if (IsMeasureMode)
            {
                int currentBar = GetBarAtTime(GameManager.InputTime);
                int targetBBar = currentBar + 1; // Snaps to the end of the currently playing bar

                int aBar = GetBarAtTime(GameManager.PracticeManager.TimeStart);

                if (targetBBar <= aBar)
                {
                    targetBBar = aBar + 1;
                }
                GameManager.PracticeManager.SetBPosition(GetTimeAtBar(targetBBar));
            }
            else
            {
                double newTime = Math.Max(GameManager.PracticeManager.TimeStart, GameManager.InputTime);
                GameManager.PracticeManager.SetBPosition(newTime);
            }

            UpdatePositionText();
        }

        public void ResetAbPositions()
        {
            GameManager.PracticeManager.ResetAbPositions();
            UpdatePositionText();
        }

        public void SelectSections()
        {
            PauseMenuManager.PopMenu();
            PauseMenuManager.PushMenu(PauseMenuManager.Menu.SelectSections);
        }

        private void UpdatePositionText()
        {
            if (_aPositionText != null)
                _aPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeStart).ToString(@"hh\:mm\:ss");

            if (_bPositionText != null)
                _bPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeEnd).ToString(@"hh\:mm\:ss");
        }

        private int GetBarAtTime(double time)
        {
            return GameManager.PracticeManager.GetBarAtTime(time);
        }

        private double GetTimeAtBar(int barIndex)
        {
            return GameManager.PracticeManager.GetTimeAtBar(barIndex);
        }
    }
}