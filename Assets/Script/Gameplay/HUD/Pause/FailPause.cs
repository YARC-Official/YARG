using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class FailPause : GenericPause
    {
        [SerializeField]
        private GameObject _separatorObject;

        protected override void OnEnable()
        {
            HandleNavigationScheme();
        }

        private async void HandleNavigationScheme()
        {
            await UniTask.WaitForSeconds(0.5f, true);
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
        }
        public void EnableNoFail(bool resume)
        {
            // It feels a bit icky reaching down into the settings like this
            SettingsManager.Settings.NoFail.SetValueWithoutNotify(NoFailMode.On);
            if (resume)
            {
                // UnfailSong already resumes, don't do it twice by calling Back()
                PauseMenuManager.PopAllMenus();
                GameManager.UnfailSong();
            }
            else
            {
                Restart();
            }
        }
    }
}