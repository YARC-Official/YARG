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
            // Intentionally leaving out the back button here
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, false));
        }

        // TODO: Make a similar option that only makes the rest of this song no fail
        //  and then resumes the song
        public void EnableNoFail()
        {
            // It feels a bit icky reaching down into the settings like this
            SettingsManager.Settings.NoFailMode.SetValueWithoutNotify(true);
            Restart();
        }
    }
}