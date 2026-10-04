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

        /* TODO-FRICK: This should be somewhere else
        public async void ShowQuickBind()
        {
            if (CurrentProfile is { GameMode: GameMode.FourLaneDrums or GameMode.ProKeys or
                GameMode.FiveLaneDrums or GameMode.EliteDrums})
            {
                var dialog = DialogManager.Instance.ShowFriendlyBindingDialog(CurrentProfile, CurrentProfile.GameMode);
                await dialog.WaitUntilClosed();
            }
            else
            {
                var dialog = DialogManager.Instance.ShowMessage("Unsupported Instrument Type",
                    "Quick binding is currently only supported for Drums and Keys.");
                await dialog.WaitUntilClosed();
            }
        }
        */
    }
}