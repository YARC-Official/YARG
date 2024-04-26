using System.IO;
using UnityEngine;
using YARG.Core.Song.Cache;
using YARG.Helpers;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.Settings
{
    public class SongManagerHeader : MonoBehaviour
    {
        [SerializeField]
        private ColoredButton _badSongsButton;

        private void Awake()
        {
            CheckBadSongsFile();
        }

        public void AddNewFolder()
        {
            SettingsManager.Settings.SongFolders.Add(string.Empty);
            SettingsMenu.Instance.RefreshAndKeepPosition();
        }

        public async void RefreshSongs()
        {
            using var context = new LoadingContext();
            await SongContainer.RunRefresh(false, context);
            SongSources.LoadSprites(context);
            SettingsMenu.Instance.RefreshAndKeepPosition();
        }

        private void CheckBadSongsFile()
        {
            _badSongsButton.gameObject.SetActive(File.Exists(PathHelper.BadSongsPath));
            
            var numErrors = CacheHandler.Progress.BadSongCount;

            if (numErrors > 0)
            {
                var errors = numErrors == 1 ? "ERROR" : "ERRORS";
                _badSongsButton.Text.text = $"{numErrors} {errors} FOUND";
            }
        }

        public void OpenBadSongs()
        {
            FileExplorerHelper.OpenFolder(PathHelper.BadSongsPath);
        }
    }
}