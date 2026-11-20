using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.ProfileInfo
{
    public class ProfileInfoMenu : MonoBehaviour
    {
        public YargProfile CurrentProfile { get; set; }

        private void OnEnable()
        {
            _ = Navigator.Instance.PushScheme(NavigationScheme.EmptyWithMusicPlayer);
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }
    }
}