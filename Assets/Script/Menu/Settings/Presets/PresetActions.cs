using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Data;
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

            // Disable the delete button for default (OOTB) presets — they
            // can't be deleted, so the button should look and act disabled.
            var preset = tab.SelectedPreset;
            if (preset is { DefaultPreset: true })
            {
                foreach (var button in GetComponentsInChildren<ColoredButton>())
                {
                    for (int i = 0; i < button.OnClick.GetPersistentEventCount(); i++)
                    {
                        if (button.OnClick.GetPersistentMethodName(i) == nameof(DeletePreset))
                        {
                            button.DisableButton();
                            break;
                        }
                    }
                }
            }
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

            // Deleting is irreversible, so confirm first (same compact dialog
            // as "Copy from note"), with the delete button disabled for a moment
            // so it can't be hit by accidental mashing. The cancel button keeps
            // its brighter "safe" color (delete is the destructive action, so it
            // gets the red default).
            PresetSubTab.ShowCompactConfirmation(
                Localize.Key("Settings.PresetSetting.Dialog.DeletePreset.Title"),
                Localize.KeyFormat("Settings.PresetSetting.Dialog.DeletePreset.Message", preset.Name),
                "Menu.Common.Delete", MenuData.Colors.CancelButton, () =>
                {
                    DialogManager.Instance.ClearDialog();

                    _tab.SelectedContent.DeletePreset(preset);
                    _tab.ResetSelectedPreset();

                    SettingsMenu.Instance.Refresh();
                },
                cancelColor: MenuData.Colors.BrightButton,
                armDelaySeconds: 2f);
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