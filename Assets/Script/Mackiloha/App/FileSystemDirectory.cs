using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mackiloha.App
{
    public class FileSystemDirectory : IDirectory
    {
        private readonly string absolutePath;

        public FileSystemDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException();

            absolutePath = Path.GetFullPath(directoryPath);
        }

        public string FullPath =>
            absolutePath;

        public string Name =>
            Path.GetFileName(absolutePath);

        public string[] GetFiles() =>
            Directory.GetFiles(absolutePath);

        public IDirectory[] GetSubDirectories() =>
            Directory.GetDirectories(absolutePath)
            .Select(x => new FileSystemDirectory(x))
            .ToArray();

        public IDirectory GetParent()
        {
            var parent = Directory.GetParent(absolutePath);
            if (parent?.FullName == null)
                return null;

            return new FileSystemDirectory(parent.FullName);
        }

        public bool IsLeaf() =>
            !Directory.GetDirectories(absolutePath).Any();

        public Stream GetStreamForFile(string fileName)
        {
            string filePath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(absolutePath, fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException();

            return File.OpenRead(filePath);
        }
    }
}
