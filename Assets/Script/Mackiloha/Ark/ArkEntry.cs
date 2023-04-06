using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mackiloha.Ark
{
    public class ArkEntry
    {
        private readonly static Regex _directoryRegex = new Regex(@"^[_\-a-zA-Z0-9]|([/][_\-a-zA-Z0-9]+)*$"); // TODO: Consider .. and . directories
        private readonly static Regex _fileRegex = new Regex(@"^[_\-a-zA-Z0-9]+[.]?[_\-a-zA-Z0-9]*$");

        public ArkEntry(string fileName, string directory)
        {
            FileName = fileName;
            Directory = GetDirectory(directory);
        }

        private string GetDirectory(string dir)
            => !string.IsNullOrEmpty(dir) ? dir : "."; // If entry is in root, use '.' path

        public string FileName { get; }
        public string Directory { get; }

        private bool IsValidPath(string text, bool directory = false)
        {
            if (directory)
                return _directoryRegex.IsMatch(text) || (text == string.Empty);

            return _fileRegex.IsMatch(text);
        }

        public string FullPath => string.IsNullOrEmpty(Directory) ? FileName : $"{Directory}/{FileName}";

        public string Extension => (!FileName.Contains('.')) ? "" : FileName.Remove(0, FileName.LastIndexOf('.') + 1);

        public override bool Equals(object obj) => (obj is ArkEntry) && ((obj as ArkEntry).FullPath == this.FullPath);
        public override int GetHashCode() => FullPath.GetHashCode();
        public override string ToString() => $"{FullPath}";
    }
}
