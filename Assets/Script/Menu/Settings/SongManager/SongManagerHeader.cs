using UnityEngine;
using YARG.Menu.Persistent;
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
            // Stop music if currently playing.
            bool isCurrentlyPlaying = GlobalVariables.AudioManager.IsPlaying;
            if (isCurrentlyPlaying)
            {
                HelpBar.Instance.MusicPlayer.PlayOrPauseClick();
            }

            // Reset Now Playing list.
            MusicPlayer.ResetPlayedList();

            LoadingManager.Instance.QueueSongRefresh(false);
            await LoadingManager.Instance.StartLoad();
            SettingsMenu.Instance.RefreshAndKeepPosition();

            // Restart music if playing before.
            if (isCurrentlyPlaying)
            {
                HelpBar.Instance.MusicPlayer.SkipClick();
            }
        }
    }
}