using System;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class PracticePause : GenericPause
    {
        [SerializeField]
        private TextMeshProUGUI aPositionText;

        [SerializeField]
        private TextMeshProUGUI bPositionText;

        protected override void GameplayAwake()
        {
            if (!GameManager.IsPractice)
            {
                Destroy(gameObject);
                return;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            UpdatePositionText();
        }

        public void SetAPosition()
        {
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
            _pauseMenuManager.OpenMenu(PauseMenuManager.Menu.SelectSections);
        }

        private void UpdatePositionText()
        {
            aPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeStart).ToString(@"hh\:mm\:ss");
            bPositionText.text = TimeSpan.FromSeconds(GameManager.PracticeManager.TimeEnd).ToString(@"hh\:mm\:ss");
        }
    }
}