using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public static partial class YargLogger
    {
        // How often the logging thread should output logs (milliseconds)
        private const int LOG_INTERVAL = 10;

        private static readonly List<BaseYargLogListener> Listeners = new();

        // Queue for log items. Maybe we should use a concurrent queue? Depends on how many threads will log at the same time
        private static readonly Queue<LogItem> LogQueue = new();

        /// <summary>
        /// The minimum level required for a <see cref="LogItem"/> to be logged.
        /// </summary>
        public static LogLevel MinimumLogLevel = LogLevel.Info;

        private static Utf16ValueStringBuilder _logBuilder;

        private static bool _isLoggingEnabled = true;

        static YargLogger()
        {
            _logBuilder = ZString.CreateStringBuilder();

            var logOutputterThread = new Thread(LogOutputter)
            {
                Name = "YargLogger Thread",
            };
            logOutputterThread.Start();
        }

        /// <summary>
        /// Add a new listener to the logger. This listener will receive all log items.
        /// </summary>
        public static void AddLogListener(BaseYargLogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Add(listener);
            }
        }

        /// <summary>
        /// Remove a listener from the logger. This listener will no longer receive log items.
        /// </summary>
        public static void RemoveLogListener(BaseYargLogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Remove(listener);
            }

            listener.Dispose();
        }

        /// <summary>
        /// This method will stop the logging thread and prevent any further log items from being queued.
        /// </summary>
        /// <remarks>
        /// This should be called when the application is shutting down to prevent any log items from being lost.
        /// </remarks>
        public static void KillLogger()
        {
            _isLoggingEnabled = false;
            FlushLogQueue();

            // Dispose of all listeners
            lock (Listeners)
            {
                foreach (var listener in Listeners)
                {
                    listener.Dispose();
                }
                Listeners.Clear();
            }
        }

        private static void LogOutputter()
        {
            // Keep thread running until logging is disabled and the queue is empty
            // In the event logging is disabled, we still want to process all remaining log items
            while (_isLoggingEnabled || LogQueue.Count > 0)
            {
                FlushLogQueue();

                // Sleep for a short time. Logs will process at most every LOG_INTERVAL milliseconds
                Thread.Sleep(LOG_INTERVAL);
            }
        }

        private static void FlushLogQueue()
        {
            lock (LogQueue)
            {
                lock (Listeners)
                {
                    while (LogQueue.TryDequeue(out var item))
                    {
                        using (item)
                        {
                            WriteLogItemToListeners(item);
                        }
                    }
                }
            }
        }

        private static void WriteLogItemToListeners(LogItem item)
        {
            // Send it to all listeners that are currently registered
            foreach (var listener in Listeners)
            {
                try
                {
                    _logBuilder.Clear();
                    listener.FormatLogItem(ref _logBuilder, item);
                    listener.WriteLogItem(ref _logBuilder, item);
                }
                catch (Exception e)
                {
                    // In the event formatting the log fails, print an error message with the exception
                    try
                    {
                        using var exceptionLog = FormatLogItem.MakeItem(
                            "Failed to format the log on this line! Refer to the exception below.\n{0}", e);
                        exceptionLog.Level = LogLevel.Error;

                        // Make sure to pass down the source information so the position
                        // of the original log is known.
                        exceptionLog.Source = item.Source;
                        exceptionLog.Method = item.Method;
                        exceptionLog.Line = item.Line;
                        exceptionLog.Time = item.Time;

                        _logBuilder.Clear();
                        listener.FormatLogItem(ref _logBuilder, exceptionLog);
                        listener.WriteLogItem(ref _logBuilder, exceptionLog);
                    }
                    catch
                    {
                        // If that fails too, just skip this log
                    }
                }
            }
        }

        private static void AddLogItemToQueue(LogLevel level, string source, int line, string method, LogItem item)
        {
            // If logging is disabled, don't queue anymore log items
            // This will usually happen when the application is shutting down
            if (!_isLoggingEnabled)
            {
                return;
            }

            item.Level = level;
            item.Source = source;
            item.Method = method;
            item.Line = line;
            item.Time = DateTime.Now;

            // Lock while enqueuing. This prevents the log outputter from processing the queue while we're adding to it
            lock (LogQueue)
            {
                LogQueue.Enqueue(item);
            }
        }
    }
}