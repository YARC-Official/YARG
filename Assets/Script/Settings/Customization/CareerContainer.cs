using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using YARG.Career;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Settings.Customization
{
    public class CareerContainer : CustomContent<CareerBase>
    {
        protected override string ContentDirectory     => "careers";
        public override    string PresetTypeStringName => "Career";

        public override IReadOnlyList<CareerBase> DefaultPresets => CareerBase.Defaults;

        public override BasePreset CopyPreset(BasePreset source, BasePreset destination)
        {
            var newPreset = (CareerBase) base.CopyPreset(source, destination);

            if (source is not CareerBase original || destination is not CareerBase)
            {
                return newPreset;
            }

            var newPath = newPreset.GetExtraContentFolder();

            if (original.DefaultPreset)
            {
                var sourcePath = Path.Combine(PathHelper.StreamingAssetsPath, "career", original.Id.ToString());
                CopyReferencedMedia(original, sourcePath, newPath);
            }
            else
            {
                var oldPath = original.GetExtraContentFolder();
                if (oldPath != null && Directory.Exists(oldPath) && newPath != null && !oldPath.Equals(newPath))
                {
                    CopyAdditionalFiles(oldPath, newPath);
                }
            }

            return newPreset;
        }

        public override void ExportPreset(BasePreset preset, string path)
        {
            if (preset is not CareerBase career || career.DefaultPreset)
            {
                return;
            }

            base.ExportPreset(preset, path);
        }

        public override BasePreset ImportPreset(string path)
        {
            if (base.ImportPreset(path) is not CareerBase career)
            {
                return null;
            }

            try
            {
                using var archive = ZipFile.OpenRead(path);
                SaveAdditionalFilesFromExport(archive, career);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to extract career media from preset archive.");
            }

            return career;
        }

        // TODO: Refactor so highwaypresetcontainer can (mostly) share these next two
        public override void DeletePreset(BasePreset preset)
        {
            if (preset is not CareerBase career)
            {
                base.DeletePreset(preset);
                return;
            }

            var extra = career.GetExtraContentFolder();
            if (extra != null && Directory.Exists(extra))
            {
                Directory.Delete(extra, true);
            }

            base.DeletePreset(preset);
        }

        public override void RenamePreset(BasePreset preset, string name)
        {
            // This is a little weird because the existing code deletes the original and renames it
            if (preset is not CareerBase career)
            {
                return;
            }

            // Rename the original extra files folder to a temp name
            var extraContentFolder = career.GetExtraContentFolder();

            if (extraContentFolder == null || !Directory.Exists(extraContentFolder))
            {
                base.RenamePreset(preset, name);
                return;
            }

            var tempExtraContentFolder = Path.Join(FullContentDirectory, Path.GetRandomFileName());
            Directory.Move(extraContentFolder, tempExtraContentFolder);

            base.RenamePreset(preset, name);

            // Now that preset has been renamed, it should have a valid path again, so move the extra files to the new folder
            extraContentFolder = career.GetExtraContentFolder();
            if (extraContentFolder == null)
            {
                YargLogger.LogFormatError(
                    "Failed to get extra content folder after renaming preset. Files were left in: {0}",
                    tempExtraContentFolder);
                return;
            }

            Directory.Move(tempExtraContentFolder, extraContentFolder);
        }

        protected override void AddAdditionalFilesToExport(BasePreset preset, ZipArchive archive)
        {
            var careerBase = (CareerBase) preset;
            var contentFolder = preset.GetExtraContentFolder();

            if (careerBase.DefaultPreset || careerBase.Path == null || contentFolder == null)
            {
                return;
            }

            var exportedFilenames = new HashSet<string>(StringComparer.Ordinal);
            AddMediaFileToExport(archive, contentFolder, careerBase.BackgroundImageName, exportedFilenames);
            foreach (var tier in careerBase.Tiers)
            {
                AddMediaFileToExport(archive, contentFolder, tier.MediaFilename, exportedFilenames);
            }
        }

        protected override void SaveAdditionalFilesFromExport(ZipArchive archive, CareerBase preset)
        {
            var contentFolder = preset.GetExtraContentFolder();
            if (contentFolder == null)
            {
                return;
            }

            Directory.CreateDirectory(contentFolder);
            ExtractMediaFileFromArchive(archive, contentFolder, preset.BackgroundImageName);

            foreach (var tier in preset.Tiers)
            {
                ExtractMediaFileFromArchive(archive, contentFolder, tier.MediaFilename);
            }
        }

        private static void CopyReferencedMedia(CareerBase career, string sourceFolder, string destinationFolder)
        {
            if (destinationFolder == null)
            {
                return;
            }

            CopyMediaFile(career.BackgroundImageName, sourceFolder, destinationFolder);
            foreach (var tier in career.Tiers)
            {
                CopyMediaFile(tier.MediaFilename, sourceFolder, destinationFolder);
            }
        }

        private static void CopyMediaFile(FileInfo mediaFile, string sourceFolder, string destinationFolder)
        {
            if (mediaFile == null)
            {
                return;
            }

            var filename = mediaFile.Name;
            var sourcePath = Path.Combine(sourceFolder, filename);
            if (!File.Exists(sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(destinationFolder);
            File.Copy(sourcePath, Path.Combine(destinationFolder, filename), true);
        }

        private static void AddMediaFileToExport(ZipArchive archive, string contentFolder, FileInfo mediaFile,
            HashSet<string> exportedFilenames)
        {
            if (mediaFile == null)
            {
                return;
            }

            var filename = mediaFile.Name;
            var filePath = Path.Combine(contentFolder, filename);
            if (File.Exists(filePath) && exportedFilenames.Add(filename))
            {
                archive.CreateEntryFromFile(filePath, filename);
            }
        }

        private static void ExtractMediaFileFromArchive(ZipArchive archive, string contentFolder, FileInfo mediaFile)
        {
            if (mediaFile == null)
            {
                return;
            }

            var filename = mediaFile.Name;
            archive.GetEntry(filename)?.ExtractToFile(Path.Combine(contentFolder, filename), true);
        }

        public void RefreshSongEntries()
        {
            foreach (var defaults in DefaultPresets)
            {
                defaults.RefreshSongEntries();
            }

            foreach (var customs in CustomPresets)
            {
                customs.RefreshSongEntries();
            }
        }
    }
}