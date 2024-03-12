using UnityEngine;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.Settings
{
    public class SongManagerHeader : MonoBehaviour
    {
        public void AddNewFolder()
        {
            SettingsManager.Settings.SongFolders.Add(string.Empty);
            SettingsMenu.Instance.RefreshAndKeepPosition();
        }

        public async void RefreshSongs()
        {
            using var context = new LoadingContext();
            await SongContainer.RunRefresh(false, context);
            SettingsMenu.Instance.RefreshAndKeepPosition();
        }
    }
}