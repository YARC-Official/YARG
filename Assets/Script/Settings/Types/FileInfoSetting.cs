using System;
using System.IO;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Settings.Types
{
    public class FileInfoSetting : AbstractSetting<FileInfo>
    {
        public override  string     AddressableName => "Setting/FileInfo";
        private readonly BasePreset _preset;
        private readonly string     _settingName;

        public FileInfoSetting(FileInfo fileInfo, BasePreset preset, string settingName,
            Action<FileInfo> onChange = null) : base(onChange)
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

            var fileName = _settingName switch
            {
                "BackgroundImage" => "background.png",
                "SideImage"       => "side.png",
                _                 => value.Name
            };

            // Copy the file into the preset's extra content folder and use that copy instead of the original.
            if (!value.Exists)
            {
                YargLogger.LogFormatError("File {0} does not exist!", value.FullName);
                return;
            }

            var presetFolder = _preset.GetExtraContentFolder();
            if (presetFolder == null)
            {
                return;
            }

            Directory.CreateDirectory(presetFolder);

            var destination = Path.Combine(presetFolder, fileName);
            if (!string.Equals(value.FullName, destination, StringComparison.OrdinalIgnoreCase))
            {
                YargLogger.LogDebug($"Copying file {value.FullName} to {destination}");
                File.Copy(value.FullName, destination, true);
            }

            _value = new FileInfo(fileName);
        }

        public override bool ValueEquals(FileInfo value)
        {
            return string.Equals(value?.Name, Value?.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}