using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;
using System.Text.RegularExpressions;

namespace Mackiloha
{
    // Just in case I port this to other platforms and something breaks
    public static class FileHelper
    {
        private static Regex DirSeparatorRegex = new Regex(@"[\/\\]");

        public static string FixSlashes(string path)
        {
            // Fix directory slash characters
            return DirSeparatorRegex.Replace(path, $"{Path.DirectorySeparatorChar}");
        }

        public static string SanitizePath(string path)
        {
            var regex = new Regex(@"\n|\r|\t");
            return regex.Replace(path, "");
        }

        public static string GetDirectory(string filePath)
        {
            return Path.GetDirectoryName(filePath);
        }

        public static string ReplaceExtension(string filePath, string extension)
        {
            return Path.ChangeExtension(filePath, extension);
        }

        public static string GetFileName(string filePath)
        {
            return Path.GetFileName(filePath);
        }

        public static string GetFileNameWithoutExtension(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        public static void CreateDirectoryIfNotExists(string filePath)
        {
            string directory = GetDirectory(filePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        public static string RemoveExtension(string filePath)
        {
            return $@"{GetDirectory(filePath)}\{GetFileNameWithoutExtension(filePath)}";
        }

        public static string[] GetFilesAtExactDepth(string dirPath, int depth)
        {
            var files = new List<string>();
            FindFilesForDepthRecursively(dirPath, depth, files);
            files.Sort();

            return files.ToArray();
        }

        private static void FindFilesForDepthRecursively(string path, int depthsLeft, List<string> foundFiles)
        {
            if (depthsLeft <= 0)
            {
                foundFiles.AddRange(Directory.GetFiles(path));
                return;
            }

            var dirs = Directory.GetDirectories(path);
            foreach (var dir in dirs)
            {
                FindFilesForDepthRecursively(dir, depthsLeft - 1, foundFiles);
            }
        }

        public static bool HasAccess(string path)
        {
            // TODO: Implement for .net standard
            DirectoryInfo info = new DirectoryInfo(path);
            //try
            //{
            //    DirectorySecurity dirAC = info.GetAccessControl(AccessControlSections.All);
            //    return true;
            //}
            //catch (PrivilegeNotHeldException)
            //{
            //    return false;
            //}

            return true;
        }

        public static string GetRelativePath(string path, string basePath)
        {
            //return Path.GetFullPath(path).Substring(Path.GetFullPath(basePath).Length + 1);
            return System.IO.Path.GetRelativePath(Path.GetFullPath(basePath), Path.GetFullPath(path));
        }

        public static byte[] GetBytes(string hex)
        {
            // TODO: Move to another location?
            var bytes = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i >> 1] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }
    }
}
