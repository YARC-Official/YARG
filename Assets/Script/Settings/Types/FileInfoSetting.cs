using System;
using System.IO;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Settings.Customization;

namespace YARG.Settings.Types
{
    public class FileInfoSetting : AbstractSetting<FileInfo>
    {
        public override  string     AddressableName => "Setting/FileInfo";
        private readonly BasePreset _preset;
        private readonly string     _settingName;

        public FileInfoSetting(FileInfo fileInfo, BasePreset preset, string settingName, Action<FileInfo> onChange = null) : base(onChange)
        {
            _preset = preset;
            _settingName = settingName;
            _value = fileInfo;
        }

        protected override void SetValue(FileInfo value)
        {
            if (value == null)
            {
                _value = null;
                return;
            }

            var imageName = _settingName switch
            {
                "BackgroundImage" => "background.png",
                "SideImage"       => "side.png",
                _                 => throw new ArgumentOutOfRangeException()
            };

            // Copy the file into the settings folder and use that copy instead of the original.
            if (!value.Exists)
            {
                YargLogger.LogFormatError("File {0} does not exist!", value.FullName);
                return;
            }

            if (_preset.Path == null)
            {
                // Do something?
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(_preset.Path);
            var presetFolder = Path.Combine(CustomContentManager.HighwayPresets.FullContentDirectory, baseName!);

            // If the preset folder doesn't exist, create it
            Directory.CreateDirectory(presetFolder);

            var newPath = Path.Combine(presetFolder, imageName);
            YargLogger.LogDebug($"Copying file {value.FullName} to {newPath}");
            File.Copy(value.FullName, newPath, true);
            _value = new FileInfo(newPath);
        }

        public override bool ValueEquals(FileInfo value)
        {
            return value.FullName == Value.FullName;
        }
    }
}