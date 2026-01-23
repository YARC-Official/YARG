using System;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class PracticePause : GenericPause
    {
        [SerializeField]
        private TextMeshProUGUI _aPositionText;

        [SerializeField]
        private TextMeshProUGUI _bPositionText;

        protected override void OnEnable()
        {
            base.OnEnable();

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