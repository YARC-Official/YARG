using System;
using TMPro;
using UnityEngine;
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

        private NavigatableBehaviour _aPositionNav;
        private NavigatableBehaviour _bPositionNav;

        protected override void OnEnable()
        {
            // Cache navigatable references for the A/B position buttons
            _aPositionNav ??= _aPositionText.GetComponentInParent<NavigatableBehaviour>();
            _bPositionNav ??= _bPositionText.GetComponentInParent<NavigatableBehaviour>();

            // Push scheme with Left/Right for position adjustment (replaces base.OnEnable scheme)
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                new NavigationScheme.Entry(MenuAction.Left, "Menu.Common.Decrease", () => AdjustSelectedPosition(-1.0)),
                new NavigationScheme.Entry(MenuAction.Right, "Menu.Common.Increase", () => AdjustSelectedPosition(1.0)),
            }, false));

            UpdatePositionText();
        }

        private void AdjustSelectedPosition(double deltaSeconds)
        {
            var selected = NavigationGroup.CurrentNavigationGroup?.SelectedBehaviour;
            if (selected == null) return;

            if (selected == _aPositionNav)
            {
                double newTime = Math.Max(0, GameManager.PracticeManager.TimeStart + deltaSeconds);
                GameManager.PracticeManager.SetAPosition(newTime);
                UpdatePositionText();
            }
            else if (selected == _bPositionNav)
            {
                double newTime = Math.Max(0, GameManager.PracticeManager.TimeEnd + deltaSeconds);
                GameManager.PracticeManager.SetBPosition(newTime);
                UpdatePositionText();
            }
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