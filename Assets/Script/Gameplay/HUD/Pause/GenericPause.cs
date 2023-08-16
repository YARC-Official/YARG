using System;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class GenericPause : MonoBehaviour
    {
        private GameManager _gameManager;

        private PauseMenuManager _pauseMenuManager;

        [SerializeField]
        private TextMeshProUGUI aPositionText;

        [SerializeField]
        private TextMeshProUGUI bPositionText;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
            _pauseMenuManager = FindObjectOfType<PauseMenuManager>();
        }

        private void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", Resume),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));

            if (_gameManager.IsPractice)
            {
                aPositionText.text = TimeSpan.FromSeconds(_gameManager.PracticeManager.TimeStart).ToString(@"hh\:mm\:ss");
                bPositionText.text = TimeSpan.FromSeconds(_gameManager.PracticeManager.TimeEnd).ToString(@"hh\:mm\:ss");
            }
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public void Resume()
        {
            _pauseMenuManager.PopMenu();
        }

        public void Restart()
        {
            _pauseMenuManager.Restart();
        }

        public void RestartInPractice()
        {
            GlobalVariables.Instance.IsPractice = true;
            _pauseMenuManager.Restart();
        }

        public void RestartInQuickPlay()
        {
            GlobalVariables.Instance.IsPractice = false;
            _pauseMenuManager.Restart();
        }

        public void SetAPosition()
        {
            _gameManager.PracticeManager.SetAPosition(_gameManager.InputTime);
            UpdatePositionText();
        }

        public void SetBPosition()
        {
            _gameManager.PracticeManager.SetBPosition(_gameManager.InputTime);
            UpdatePositionText();
        }

        public void ResetAbPositions()
        {
            _gameManager.PracticeManager.ResetAbPositions();
            UpdatePositionText();
        }

        public void SelectSections()
        {
            _pauseMenuManager.OpenMenu(PauseMenuManager.Menu.SelectSections);
        }

        public void BackToLibrary()
        {
            _pauseMenuManager.Quit();
        }

        private void UpdatePositionText()
        {
            aPositionText.text = TimeSpan.FromSeconds(_gameManager.PracticeManager.TimeStart).ToString(@"hh\:mm\:ss");
            bPositionText.text = TimeSpan.FromSeconds(_gameManager.PracticeManager.TimeEnd).ToString(@"hh\:mm\:ss");
        }
    }
}