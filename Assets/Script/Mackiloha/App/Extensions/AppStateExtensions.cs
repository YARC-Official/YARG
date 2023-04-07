using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mackiloha.App.Metadata;
using Mackiloha.IO;
using Mackiloha.Milo2;

namespace Mackiloha.App.Extensions
{
    public static class AppStateExtensions
    {
        public static MiloObjectDir OpenMiloFile(this AppState state, string miloPath)
        {
            MiloFile miloFile;
            using (var fileStream = state.GetWorkingDirectory().GetStreamForFile(miloPath))
            {
                miloFile = MiloFile.ReadFromStream(fileStream);
            }

            var serializer = state.GetSerializer();

            MiloObjectDir milo;
            using (var miloStream = new MemoryStream(miloFile.Data))
            {
                milo = serializer.ReadFromStream<MiloObjectDir>(miloStream);
            }

            return milo;
        }

        public static IDirectory GetRoot(this AppState state)
        {
            // TODO: Set from user input
            IDirectory dir = state.GetWorkingDirectory();
            IDirectory[] subDirs;

            do
            {
                subDirs = dir.GetSubDirectories();

                if (subDirs.Any(x =>
                    string.Equals(x.Name, "songs",
                        StringComparison.CurrentCultureIgnoreCase)))
                    return dir;

                dir = dir.GetParent();

            } while (dir != null);

            return null;
        }

        public static void ExtractMiloContents(this AppState state, string miloPath, string outputDir, bool convertTextures)
        {
            var milo = OpenMiloFile(state, miloPath);
            milo.Name = Path.GetFileName(miloPath);

            milo.ExtractToDirectory(outputDir, convertTextures, state);
        }

        private static Platform GuessPlatform(string fileName, int version, bool endian)
        {
            // TODO: Either move or remove
            var ext = fileName?.Split('_')?.LastOrDefault()?.ToLower();

            return (ext, version) switch
            {
                ("gc", _) => Platform.GC,
                ("ps2", _) => Platform.PS2,
                ("ps3", _) => Platform.PS3,
                ("ps4", _) => Platform.PS3,
                ("wii", _) => Platform.Wii,
                var (p, v) when p == "xbox" && v <= 24 => Platform.XBOX,
                var (p, v) when p == "xbox" && v >= 25 => Platform.X360,
                // TODO: Determine when XB1
                _ => Platform.PS2
            };
        }

        public static void BuildMiloArchive(this AppState state, string dirPath, string outputPath)
        {
            var directoryTypes = new[]
            {
                // GH2 PS2
                "BandCharacter",
                "BandCrowdMeterDir",
                "BandLeadMeter",
                "BandScoreDisplay",
                "BandStarMeterDir",
                "BandStreakDisplay",
                "CharClipSet",
                "Character",
                "ObjectDir",
                "PanelDir",
                "RndDir",
                "WorldDir",
                "WorldFx"
                // TODO: Add GH2 360 dir types?
            };

            if (!Directory.Exists(dirPath))
                throw new DirectoryNotFoundException();

            // Only finds files at 2nd depth
            var allFiles = FileHelper.GetFilesAtExactDepth(dirPath, 1);

            var groupedFiles = allFiles
                .GroupBy(x => x
                    .Split(Path.DirectorySeparatorChar)
                    .Reverse()
                    .Skip(1)
                    .First(), y => y)
                .ToDictionary(x => x.Key, y => y.ToList());

            var metaRegex = new Regex("[.]meta[.]json$", RegexOptions.IgnoreCase);

            var miloDir = new MiloObjectDir();
            var miloTypes = groupedFiles.Keys.ToList();
            MiloObjectBytes dirEntry = null;

            if (state.SystemInfo.Version >= 24)
            {
                // Finds rnd.json file
                var dirMetaPath = Path.Combine(dirPath, "rnd.json");
                string dirName = null;
                string dirType = null;
                if (File.Exists(dirMetaPath))
                {
                    // Deserializes rnd meta
                    var dirMeta = JsonSerializer.Deserialize<DirectoryMeta>(File.ReadAllText(dirMetaPath), state.JsonSerializerOptions);
                    dirName = dirMeta.Name;
                    dirType = dirMeta.Type;
                }

                // Guess directory type
                dirType = dirType ?? directoryTypes
                    .Intersect(miloTypes)
                    .Where(x => groupedFiles[x].Count == 1)
                    .FirstOrDefault();

                if (dirType is null)
                {
                    throw new Exception($"Can't determine directory type for milo");
                }

                string dirEntryPath;
                if (!groupedFiles.TryGetValue(dirType, out var dirFiles))
                {
                    throw new Exception($"Can't find file for \"{dirType}\" type");
                }

                if (string.IsNullOrEmpty(dirName))
                {
                    // Just use whatever is available
                    dirEntryPath = dirFiles
                        .First();

                    Console.WriteLine($"Explicit directory name not given for \"{dirType}\" type, using \"{Path.GetFileName(dirEntryPath)}\"");
                }
                else
                {
                    // Filter by given file name
                    dirEntryPath = dirFiles
                        .FirstOrDefault(x => string.Equals(Path.GetFileName(x), dirName, StringComparison.CurrentCultureIgnoreCase));

                    if (dirEntryPath is null)
                    {
                        // Just use whatever is available
                        dirEntryPath = dirFiles
                            .First();

                        Console.WriteLine($"Can't find file with name \"{dirName ?? "(null)"}\" for \"{dirType}\" type, using \"{Path.GetFileName(dirEntryPath)}\" instead");
                    }
                }

                dirEntry = new MiloObjectBytes(dirType)
                {
                    Name = Path.GetFileName(dirEntryPath),
                    Data = File.ReadAllBytes(dirEntryPath)
                };

                miloDir.Extras.Add("DirectoryEntry", dirEntry);
            }
            else if (state.SystemInfo.Version < 24)
            {
                // GH1 (and others?)
                miloDir.Extras.Add("ExternalResources", new List<string>());
            }
            
            foreach (var type in miloTypes)
            {
                var metaPaths = groupedFiles[type]
                    .Where(x => metaRegex.IsMatch(x))
                    .ToList();

                var filePaths = groupedFiles[type]
                    .Except(metaPaths)
                    .ToList();

                
                if (type == "Tex")
                {
                    var defaultTexMeta = TexMeta.DefaultFor(state.SystemInfo.Platform);
                    var imageRegex = new Regex("[.]((bmp)|(jpg)|(jpeg)|(png))$", RegexOptions.IgnoreCase); // TODO: Support more formats
                    var texRegex = new Regex("[.]tex$", RegexOptions.IgnoreCase);

                    var texMetas = metaPaths
                        .ToDictionary(x => metaRegex.Replace(Path.GetFileName(x), ""), y => JsonSerializer.Deserialize<TexMeta>(File.ReadAllText(y), state.JsonSerializerOptions));

                    var uniquePaths = filePaths
                        .GroupBy(x => Path.GetFileNameWithoutExtension(x))
                        .ToDictionary(x => x.Key, y => y.ToList());

                    foreach (var uniqueEntry in uniquePaths.Keys)
                    {
                        // TODO: Order by preferred image format?
                        var supportedImagePath = uniquePaths[uniqueEntry]
                            .FirstOrDefault(x => imageRegex.IsMatch(x));

                        if (supportedImagePath == null)
                        {
                            var rawFilePath = uniquePaths[uniqueEntry]
                                .FirstOrDefault(x => !imageRegex.IsMatch(x));

                            // Just copy the raw file
                            var entry = new MiloObjectBytes(type)
                            {
                                Name = Path.GetFileName(rawFilePath),
                                Data = File.ReadAllBytes(rawFilePath)
                            };

                            miloDir.Entries.Add(entry);
                            continue;
                        }

                        var texMeta = texMetas.ContainsKey(uniqueEntry)
                            ? texMetas[uniqueEntry]
                            : defaultTexMeta;

                        var texBitmap = TextureExtensions.TexFromImage(supportedImagePath, state.SystemInfo);
                        miloDir.Entries.Add(texBitmap);
                    }

                    continue;
                }

                foreach (var entryPath in filePaths)
                {
                    var entryName = Path.GetFileName(entryPath);

                    // Skip writing directory entry
                    if (dirEntry?.Type == type && dirEntry?.Name == entryName)
                        continue;

                    var entry = new MiloObjectBytes(type)
                    {
                        Name = entryName,
                        Data = File.ReadAllBytes(entryPath)
                    };

                    miloDir.Entries.Add(entry);
                }
            }

            var serializer = state.GetSerializer();

            var miloFile = new MiloFile
            {
                Data = serializer.WriteToBytes(miloDir)
            };


            miloFile.WriteToFile(outputPath);
        }

        public static MiloSerializer GetSerializer(this AppState state) => new MiloSerializer(state.SystemInfo);
    }
}
