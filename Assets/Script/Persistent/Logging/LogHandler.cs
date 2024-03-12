using System;
using System.IO;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Logging
{
    public static class LogHandler
    {

        private static bool _isInitialized;

        private static string _logsDirectory;

        public static void Init()
        {
            if (_isInitialized)
            {
                return;
            }

            _logsDirectory = Path.Combine(PathHelper.PersistentDataPath, "logs");
            Directory.CreateDirectory(_logsDirectory);

            // Add log listeners here
            YargLogger.AddLogListener(new UnityEditorLogListener(new UnityEditorLogFormat()));
            YargLogger.AddLogListener(new FileYargLogListener(GetLogPath(), new StandardYargLogFormatter()));

            _isInitialized = true;
        }

        private static string GetLogPath()
        {
            var date = DateTime.Today;

            var file = $"{date:yyyy-MM-dd}";

            int i = 1;
            while (File.Exists(Path.Combine(_logsDirectory, file + ".log")))
            {
                file = $"{date:yyyy-MM-dd}_{i}";
                i++;
            }

            return Path.Combine(_logsDirectory, file + ".log");
        }
    }
}