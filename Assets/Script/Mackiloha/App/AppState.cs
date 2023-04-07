using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mackiloha.IO;

namespace Mackiloha.App
{
    public class AppState
    {
        private readonly IDirectory _workingDirectory;
        private SystemInfo _systemInfo;

        public AppState(string workingDirectory)
        {
            _workingDirectory = new FileSystemDirectory(workingDirectory);
            _systemInfo.Version = 10; // GH1
            _systemInfo.Platform = Platform.PS2;

            JsonSerializerOptions = new JsonSerializerOptions()
            {
                IgnoreNullValues = true,
                WriteIndented = true
            };

            JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public void UpdateSystemInfo(SystemInfo info)
        {
            // TODO: Add version verification?
            _systemInfo = info;
        }

        public IDirectory GetWorkingDirectory() => _workingDirectory;
        public SystemInfo SystemInfo => _systemInfo;

        public JsonSerializerOptions JsonSerializerOptions { get; }

        public static AppState FromFile(string filePath) => new AppState(Path.GetDirectoryName(Path.GetFullPath(filePath)));
    }
}
