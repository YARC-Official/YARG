using System.IO;
using UnityEngine;
using YARG.Helpers;
using YARG.Menu.Persistent;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetActions : MonoBehaviour
    {
        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;
        }

        public void RenamePreset()
        {
            var preset = _tab.SelectedPreset;

            if (preset.DefaultPreset) return;

            DialogManager.Instance.ShowRenameDialog("Rename Preset", value =>
            {
                _tab.SelectedContent.RenamePreset(preset, value);

                SettingsMenu.Instance.Refresh();
            });
        }

        public void CopyPreset()
        {
            var preset = _tab.SelectedPreset;

            var copy = preset.CopyWithNewName($"Copy of {preset.Name}");
            _tab.SelectedContent.CopyPreset(preset, copy);
            _tab.SelectedPreset = copy;

            SettingsMenu.Instance.Refresh();
        }

        public void DeletePreset()
        {
            var preset = _tab.SelectedPreset;

            if (preset.DefaultPreset) return;

            _tab.SelectedContent.DeletePreset(preset);
            _tab.ResetSelectedPreset();

            SettingsMenu.Instance.Refresh();
        }

        public void ImportPreset()
        {
            FileExplorerHelper.OpenChooseFile(null, "preset", path =>
            {
                var preset = _tab.SelectedContent.ImportPreset(path);
                if (preset is null) return;

                _tab.SelectedPreset = preset;

                SettingsMenu.Instance.Refresh();
            });
        }

        public void ExportPreset()
        {
            var preset = _tab.SelectedPreset;

            if (preset.DefaultPreset) return;

            // Ask the user for an ending location
            FileExplorerHelper.OpenSaveFile(null, preset.Name, "preset", path => {
                // Delete the file if it already exists
                if (File.Exists(path)) File.Delete(path);

                _tab.SelectedContent.ExportPreset(preset, path);
            });
        }
    }
}