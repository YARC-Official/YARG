using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using YARG.Career;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Settings.Customization
{
    public class CareerContainer : CustomContent<CareerBase>
    {
        protected override string ContentDirectory => "careers";
        public override string PresetTypeStringName => "Career";

        public override IReadOnlyList<CareerBase> DefaultPresets { get; } = new List<CareerBase>();

        public override BasePreset CopyPreset(BasePreset source, BasePreset destination)
        {
            var newPreset = (CareerBase) base.CopyPreset(source, destination);

            if (source is not CareerBase original || destination is not CareerBase)
            {
                return newPreset;
            }

            var oldPath = original.GetExtraContentFolder();
            var newPath = newPreset.GetExtraContentFolder();

            if (oldPath != null && Directory.Exists(oldPath) && newPath != null && !oldPath.Equals(newPath))
            {
                CopyAdditionalFiles(oldPath, newPath);
            }

            return newPreset;
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
                YargLogger.LogFormatError("Failed to get extra content folder after renaming preset. Files were left in: {0}", tempExtraContentFolder);
                return;
            }

            Directory.Move(tempExtraContentFolder, extraContentFolder);
        }

        protected override void AddAdditionalFilesToExport(BasePreset preset, ZipArchive archive)
        {
            var careerBase = (CareerBase) preset;
            var contentFolder = preset.GetExtraContentFolder();

            if (careerBase.Path == null || contentFolder == null)
            {
                return;
            }

            foreach (var tier in careerBase.Tiers)
            {
                if (string.IsNullOrWhiteSpace(tier.MediaFilename))
                {
                    continue;
                }

                var mediaPath = Path.Combine(contentFolder, tier.MediaFilename);
                if (File.Exists(mediaPath))
                {
                    archive.CreateEntryFromFile(contentFolder, Path.GetFileName(mediaPath));
                }
            }
        }

        protected override void SaveAdditionalFilesFromExport(ZipArchive archive, CareerBase preset)
        {
            if (preset.Path == null)
            {
                return;
            }

            var contentFolder = preset.GetExtraContentFolder();

            if (contentFolder == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(preset.BackgroundImageName))
            {
                var bgEntry = archive.GetEntry(preset.BackgroundImageName);
                bgEntry?.ExtractToFile(Path.Combine(contentFolder, preset.BackgroundImageName), true);
            }

            foreach (var tier in preset.Tiers)
            {
                if (string.IsNullOrWhiteSpace(tier.MediaFilename))
                {
                    continue;
                }

                var entry = archive.GetEntry(tier.MediaFilename);

                // TODO: If preset.Path isn't what we think it is, this will not work
                entry?.ExtractToFile(Path.Combine(contentFolder, tier.MediaFilename), true);
            }
        }
    }
}