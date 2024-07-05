using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.Settings
{
    public class SettingsDirectory : MonoBehaviour
    {
        private static List<string> SongFolders => SettingsManager.Settings.SongFolders;

        [SerializeField]
        private TextMeshProUGUI _pathText;
        [SerializeField]
        private TextMeshProUGUI _songCountText;

        private int _index;

        public void SetIndex(int index)
        {
            _index = index;
            RefreshText();
        }

        private void RefreshText()
        {
            if (string.IsNullOrEmpty(SongFolders[_index]))
            {
                _pathText.text = Localize.Key("Menu.Settings.NoFolder");
                _songCountText.text = string.Empty;
            }
            else
            {
                _pathText.text = SongFolders[_index];

                int songCount = 0;
                foreach (var song in SongContainer.Songs)
                {
                    if (song.Directory.StartsWith(SongFolders[_index]))
                    {
                        songCount++;
                    }
                }

                if (songCount == 0)
                {
                    _songCountText.text = Localize.Key("Menu.Settings.ScanNeeded");
                }
                else
                {
                    _songCountText.text = Localize.KeyFormat("Menu.Settings.SongCount", songCount);
                }
            }
        }

        public void Remove()
        {
            // Remove the element
            SongFolders.RemoveAt(_index);

            // Refresh
            SettingsMenu.Instance.Refresh();
        }

        public void Browse()
        {
            var startingDir = SongFolders[_index];
            FileExplorerHelper.OpenChooseFolder(startingDir, folder =>
            {
                SongFolders[_index] = folder;
                RefreshText();
            });
        }
    }
}