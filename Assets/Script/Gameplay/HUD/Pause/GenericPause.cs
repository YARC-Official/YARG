using System;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Replays;

namespace YARG.Gameplay.HUD
{
    public class GenericPause : GameplayBehaviour
    {
        protected bool HasNavigationScheme { get; set; }
        protected override bool DisableUntilSongStarts => false;

        protected PauseMenuManager PauseMenuManager { get; private set; }

        protected override void GameplayAwake()
        {
            PauseMenuManager = FindAnyObjectByType<PauseMenuManager>();
        }

        protected virtual void OnEnable()
        {
            if (Navigator.Instance == null)
            {
                return;
            }

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Start, "Menu.Pause.Generic.Resume", Back, hide: true),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
            HasNavigationScheme = true;
        }

        protected virtual void OnDisable()
        {
            if (HasNavigationScheme)
            {
                if (Navigator.Instance != null)
                {
                    Navigator.Instance.PopScheme();
                }

                HasNavigationScheme = false;
            }
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
            GlobalVariables.State.SavedInputTime = GameManager.InputTime;
            PauseMenuManager.Restart();
        }

        public void SaveReplay()
        {
            bool succeeded = false;
            try
            {
                succeeded = GameManager.SaveReplay(GameManager.InputTime, ReplayContainer.ReplayDirectory) != null;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to save replay mid-song");
            }

            if (succeeded)
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
