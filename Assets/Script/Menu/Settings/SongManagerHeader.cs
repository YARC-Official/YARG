using UnityEngine;
using YARG.Settings;

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
            LoadingManager.Instance.QueueSongRefresh(false);
            await LoadingManager.Instance.StartLoad();
        }
    }
}