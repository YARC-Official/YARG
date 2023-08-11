using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using SFB;
using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu.Settings
{
    public class SettingsDirectory : MonoBehaviour
    {
        private static List<string> SongFolders => SettingsManager.Settings.SongFolders;

        [SerializeField]
        private TextMeshProUGUI pathText;

        [SerializeField]
        private TextMeshProUGUI songCountText;

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
                pathText.text = "<i>No Folder</i>";
                songCountText.text = "";
            }
            else
            {
                pathText.text = SongFolders[_index];

                int songCount = 0;
                foreach (var song in GlobalVariables.Instance.SongContainer.Songs)
                    if (song.Directory.StartsWith(SongFolders[_index]))
                        ++songCount;

                if (songCount == 0)
                {
                    songCountText.text = "<alpha=#60>SCAN NEEDED";
                }
                else
                {
                    songCountText.text = $"{songCount} <alpha=#60>SONGS";
                }
            }
        }

        public void Remove()
        {
            // Remove the element
            SongFolders.RemoveAt(_index);

            // Refresh
            SettingsMenu.Instance.UpdateSongFolderManager();
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