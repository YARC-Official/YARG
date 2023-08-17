using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class GenericPause : GameplayBehaviour
    {
        protected PauseMenuManager _pauseMenuManager { get; private set; }

        protected override void GameplayAwake()
        {
            _pauseMenuManager = FindObjectOfType<PauseMenuManager>();
        }

        protected virtual void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", Resume),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public virtual void Resume()
        {
            _pauseMenuManager.PopMenu();
        }

        public virtual void Restart()
        {
            _pauseMenuManager.Restart();
        }

        public void TogglePractice()
        {
            GlobalVariables.Instance.IsPractice = !GlobalVariables.Instance.IsPractice;
            _pauseMenuManager.Restart();
        }

        public void BackToLibrary()
        {
            _pauseMenuManager.Quit();
        }
    }
}