using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Settings.Customization
{
    public class HighwayPresetContainer : CustomContent<HighwayPreset>
    {
        protected override string ContentDirectory => "highwayPresets";

        public override string PresetTypeStringName => "HighwayPreset";

        public override IReadOnlyList<HighwayPreset> DefaultPresets => HighwayPreset.Defaults;

        public override BasePreset CopyPreset(BasePreset source, BasePreset copy)
        {
            var newPreset = (HighwayPreset) base.CopyPreset(source, copy);

            if (source is not HighwayPreset sourcePreset || copy is not HighwayPreset destinationPreset)
            {
                return newPreset;
            }

            var oldPath = sourcePreset.GetExtraContentFolder();
            var newPath = newPreset.GetExtraContentFolder();

            if (oldPath != null && Directory.Exists(oldPath) && newPath != null && !oldPath.Equals(newPath))
            {
                CopyAdditionalFiles(oldPath, newPath);
            }

            return newPreset;
        }

        public override void DeletePreset(BasePreset preset)
        {
            if (preset is not HighwayPreset highwayPreset)
            {
                base.DeletePreset(preset);
                return;
            }

            var extraContentFolder = highwayPreset.GetExtraContentFolder();

            if (extraContentFolder != null && Directory.Exists(extraContentFolder))
            {
                Directory.Delete(extraContentFolder, true);
            }

            base.DeletePreset(preset);
        }

        public override void RenamePreset(BasePreset preset, string name)
        {
            // This is a little weird because the existing code deletes the original and renames it
            if (preset is not HighwayPreset highwayPreset)
            {
                return;
            }

            // Rename the original extra files folder to a temp name
            var extraContentFolder = highwayPreset.GetExtraContentFolder();

            if (extraContentFolder == null || !Directory.Exists(extraContentFolder))
            {
                base.RenamePreset(preset, name);
                return;
            }

            var tempExtraContentFolder = Path.Join(FullContentDirectory, Path.GetRandomFileName());
            Directory.Move(extraContentFolder, tempExtraContentFolder);

            base.RenamePreset(preset, name);

            // Now that preset has been renamed, it should have a valid path again, so move the extra files to the new folder
            extraContentFolder = highwayPreset.GetExtraContentFolder();
            if (extraContentFolder == null)
            {
                YargLogger.LogFormatError("Failed to get extra content folder after renaming preset. Files were left in: {0}", tempExtraContentFolder);
                return;
            }

            Directory.Move(tempExtraContentFolder, extraContentFolder);
        }

        protected override void AddAdditionalFilesToExport(BasePreset preset, ZipArchive archive)
        {
            base.AddAdditionalFilesToExport(preset, archive);
            var highwayPreset = (HighwayPreset) preset;
            var backgroundImage = highwayPreset.BackgroundImage;
            var sideImage = highwayPreset.SideImage;
            if (backgroundImage is { Exists: true })
            {
                archive.CreateEntryFromFile(backgroundImage.FullName, "background.png");
            }
            if (sideImage is { Exists: true })
            {
                archive.CreateEntryFromFile(sideImage.FullName, "side.png");
            }
        }

        protected override void SaveAdditionalFilesFromExport(ZipArchive archive, HighwayPreset preset)
        {
            ZipArchiveEntry baseEntry = null;
            ZipArchiveEntry sideEntry = null;

            if (preset.BackgroundImage == null && preset.SideImage == null)
            {
                return;
            }

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("background.png"))
                {
                    baseEntry = entry;
                }

                if (entry.FullName.EndsWith("side.png"))
                {
                    sideEntry = entry;
                }
            }

            if (baseEntry == null || sideEntry == null)
            {
                return;
            }

            // Get the filename of this preset
            var filename = GetFileNameForPreset(preset);
            // We actually want the base name without extension
            var baseName = Path.GetFileNameWithoutExtension(filename);
            // Create a folder for the images if it doesn't already exist
            var imagePath = Path.Join(FullContentDirectory, baseName);
            Directory.CreateDirectory(imagePath);

            // Save the images
            baseEntry.ExtractToFile(Path.Join(imagePath, "background.png"), true);
            sideEntry.ExtractToFile(Path.Join(imagePath, "side.png"), true);

            // Rewrite paths to the images
            preset.BackgroundImage = new FileInfo(Path.Join(imagePath, "background.png"));
            preset.SideImage = new FileInfo(Path.Join(imagePath, "side.png"));
        }

        private static void CopyAdditionalFiles(string source, string destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            if (!Directory.Exists(source))
            {
                YargLogger.LogFormatError("Source directory does not exist: {0}", source);
            }

            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Join(destination, Path.GetFileName(file)));
            }
        }
    }
}