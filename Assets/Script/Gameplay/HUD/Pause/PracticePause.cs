using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class PracticePause : GenericPause
    {
        [SerializeField]
        private TextMeshProUGUI _aPositionText;

        [SerializeField]
        private TextMeshProUGUI _bPositionText;

        private const float SCROLL_TIME = 1f / 60f;
        private const double SCROLL_ADJUST_SECONDS = 5.0;

        private NavigatableBehaviour _aPositionNav;
        private NavigatableBehaviour _bPositionNav;
        private float _scrollTimer;

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
            Navigator.Instance.NavigationEvent += OnNavigationEvent;

            UpdatePositionText();
        }

        protected override void OnDisable()
        {
            Navigator.Instance.NavigationEvent -= OnNavigationEvent;
            base.OnDisable();
        }

        private void OnNavigationEvent(NavigationContext ctx)
        {
            switch (ctx.Action)
            {
                case MenuAction.Left:
                    AdjustSelectedPosition(-1.0);
                    break;
                case MenuAction.Right:
                    AdjustSelectedPosition(1.0);
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

            var delta = Mouse.current.scroll.ReadValue().y * Time.unscaledDeltaTime;

            if (delta > 0f)
            {
                AdjustSelectedPosition(SCROLL_ADJUST_SECONDS);
                _scrollTimer = SCROLL_TIME;
            }
            else if (delta < 0f)
            {
                AdjustSelectedPosition(-SCROLL_ADJUST_SECONDS);
                _scrollTimer = SCROLL_TIME;
            }
        }

        private void AdjustSelectedPosition(double deltaSeconds)
        {
            var group = NavigationGroup.CurrentNavigationGroup;
            if (group == null)
            {
                return;
            }

            var selected = group.SelectedBehaviour;
            if (selected == null)
            {
                return;
            }

            if (selected == _aPositionNav)
            {
                double newTime = Math.Min(GameManager.SongLength,
                    Math.Max(0, GameManager.PracticeManager.TimeStart + deltaSeconds));
                GameManager.PracticeManager.SetAPosition(newTime);
            }
            else if (selected == _bPositionNav)
            {
                double newTime = Math.Min(GameManager.SongLength,
                    Math.Max(0, GameManager.PracticeManager.TimeEnd + deltaSeconds));
                GameManager.PracticeManager.SetBPosition(newTime);
            }
            else
            {
                return;
            }

            UpdatePositionText();
        }

        public void SetAPosition()
        {
            // Set time relative to the strikeline instead of the hit window
            GameManager.PracticeManager.SetAPosition(GameManager.InputTime);
            UpdatePositionText();
        }

        public void SetBPosition()
        {
            GameManager.PracticeManager.SetBPosition(GameManager.InputTime);
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
            _aPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeStart).ToString(@"hh\:mm\:ss");
            _bPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeEnd).ToString(@"hh\:mm\:ss");
        }
    }
}