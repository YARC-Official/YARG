using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class GenericPause : GameplayBehaviour
    {
        protected PauseMenuManager PauseMenuManager { get; private set; }

        protected override void GameplayAwake()
        {
            PauseMenuManager = FindObjectOfType<PauseMenuManager>();
        }

        protected override void GameplayEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", Resume),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
        }

        protected override void GameplayDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public virtual void Resume()
        {
            PauseMenuManager.PopMenu();
        }

        public virtual void Restart()
        {
            PauseMenuManager.Restart();
        }

        public void TogglePractice()
        {
            GlobalVariables.Instance.IsPractice = !GlobalVariables.Instance.IsPractice;
            PauseMenuManager.Restart();
        }

        public void SaveReplay()
        {
            GameManager.SaveReplay(GameManager.InputTime);
        }

        public void BackToLibrary()
        {
            PauseMenuManager.Quit();
        }
    }
}