using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        private GameManager _gameManager;

        [SerializeField]
        private GameObject _changeSectionButton;

        private void Start()
        {
            if (!_gameManager.IsPractice)
                Destroy(_changeSectionButton);
        }

        public void Resume()
        {
            _gameManager.Resume();
        }

        public void ChangeSection()
        {
            if (!_gameManager.IsPractice)
                return;

            _gameManager.PracticeManager.DisplayPracticeMenu();
        }

        public void Quit()
        {
            _gameManager.QuitSong();
        }
    }
}