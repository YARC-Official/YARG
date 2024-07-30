using System;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Gameplay.HUD
{
    public class GenericPause : GameplayBehaviour
    {
        protected PauseMenuManager PauseMenuManager { get; private set; }

        protected override void GameplayAwake()
        {
            PauseMenuManager = FindObjectOfType<PauseMenuManager>();
        }

        protected virtual void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public virtual void Back()
        {
            PauseMenuManager.PopAllMenusWithResume();
        }

        public virtual void Restart()
        {
            PauseMenuManager.Restart();
        }

        public void TogglePractice()
        {
            GlobalVariables.State.IsPractice = !GlobalVariables.State.IsPractice;
            PauseMenuManager.Restart();
        }

        public void SaveReplay()
        {
            bool failed = false;

            try
            {
                var output = GameManager.SaveReplay(GameManager.InputTime, false);

                if (output is null)
                {
                    failed = true;
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to save replay mid-song");

                failed = true;
            }

            if (!failed)
            {
                DialogManager.Instance.ShowMessage("Replay Saved",
                    "The replay was successfully saved mid-song. This replay can be accessed in the " +
                    "\"Imported Songs\" tab in the \"History\" menu.");
            }
            else
            {
                DialogManager.Instance.ShowMessage("Failed to Save Replay",
                    "The replay was unable to be saved mid-song. This could be because the replay only had bots " +
                    "or an error occurred. Please check the logs for more info.");
            }
        }

        public void BackToLibrary()
        {
            PauseMenuManager.Quit();
        }

        public void OpenQuickSettings()
        {
            PauseMenuManager.PushMenu(PauseMenuManager.Menu.QuickSettings);
        }
    }
}
