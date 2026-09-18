using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VLCUnity.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VLCUnity.Editor")]

namespace LibVLCSharp
{
    public static class VLCUnityLogger
    {
        private const uint DefaultLogColor = 0xD2D2D2FF;
        private const int FileQueueCapacity = 4096;
        private const int FileBatchCapacity = 256;

        private static readonly object _eventLock = new();
        private static readonly object _nativeCallbackLock = new();
        private static readonly WaitCallback _refreshNativeCallbackCallback = _ => RefreshNativeLogCallback();
        private static Action<string> _logReceived;

        /// <summary>
        /// Raised on the thread that produced the log. Subscriber exceptions are isolated.
        /// </summary>
        public static event Action<string> LogReceived
        {
            add
            {
                lock (_eventLock)
                    _logReceived += value;

                RefreshNativeLogCallback();
            }
            remove
            {
                lock (_eventLock)
                    _logReceived -= value;

                // A subscriber may remove itself from inside a native callback.
                // Clear asynchronously because the native side waits for active
                // callbacks to finish before accepting a null callback.
                ThreadPool.QueueUserWorkItem(_refreshNativeCallbackCallback);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LogCallback([MarshalAs(UnmanagedType.LPUTF8Str)] string message, uint hexColor);

        [DllImport(OnLoad.UnityPlugin, CallingConvention = CallingConvention.Winapi)]
        private static extern void SetLogCallback(LogCallback callback);

        private static readonly object _lifecycleLock = new();
        private static readonly object _libVLCHookLock = new();
        private static readonly HashSet<LibVLC> _hookedLibVLCInstances = new();
        private static readonly SendOrPostCallback _unityEventCallback = DispatchLogToUnityEvent;

        // Assigned once and never cleared: the native side may still call it
        // briefly after deregistration times out. See DisableNativeLogCallback.
        private static readonly LogCallback _logCallback = HandleNativeLog;
        private static FileLogWriter _fileWriter;
        private static SynchronizationContext _mainThreadContext;
        private static UnityEvent<string> _unityLogReceivedEvent;
        private static bool _includeLibVLCEngineLogs;
        private static bool _includeNativeRenderingLogs;
        private static bool _writeToUnityConsole;
        private static bool _nativeCallbackRegistered;
        private static bool _nativeCallbackRefreshEnabled;

        internal static VLCLogSettings _settings;
#if UNITY_INCLUDE_TESTS
        internal static Action<bool> NativeCallbackSetterForTests;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Initialize()
        {
            // SubsystemRegistration also runs when entering Play Mode with domain reload
            // disabled. Tear down any state retained from the previous session before
            // replacing delegates that may still be registered with native code.
            OnQuit();

            _mainThreadContext = SynchronizationContext.Current;

            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;

            _settings = Resources.Load<VLCLogSettings>(VLCLogSettings.ResourceName);

            if (_settings == null)
            {
                _settings = ScriptableObject.CreateInstance<VLCLogSettings>();
                _settings.hideFlags = HideFlags.HideAndDontSave;
            }

            ApplySettings(_settings);

            EnableAndRefreshNativeLogCallback();

            // VLCMediaPlayer.LibVLC is retained when domain reload is disabled.
            HookLibVLC(VLCMediaPlayer.LibVLC);
        }

        internal static void OnQuit()
        {
            Application.quitting -= OnQuit;

            lock (_eventLock)
                _logReceived = null;

            UnhookAllLibVLCInstances();
            DisableNativeLogCallback();
            StopFileWriter();
            _mainThreadContext = null;
            _unityLogReceivedEvent = null;
            _includeLibVLCEngineLogs = false;
            _includeNativeRenderingLogs = false;
            _writeToUnityConsole = false;
            _settings = null;
        }

        internal static void HookLibVLC(LibVLC libVLC)
        {
            if (libVLC == null || !ShouldCaptureLibVLCLogs())
                return;

            lock (_libVLCHookLock)
            {
                if (_hookedLibVLCInstances.Add(libVLC))
                    libVLC.Log += OnLibVLCLog;
            }
        }

        internal static void UnhookLibVLC(LibVLC libVLC)
        {
            if (libVLC == null)
                return;

            lock (_libVLCHookLock)
            {
                if (_hookedLibVLCInstances.Remove(libVLC))
                    libVLC.Log -= OnLibVLCLog;
            }
        }

        private static void UnhookAllLibVLCInstances()
        {
            lock (_libVLCHookLock)
            {
                foreach (LibVLC libVLC in _hookedLibVLCInstances)
                    libVLC.Log -= OnLibVLCLog;

                _hookedLibVLCInstances.Clear();
            }
        }

        private static void OnLibVLCLog(object sender, LogEventArgs args)
        {
            try
            {
                Log($"[LibVLC] {args.FormattedLog}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        private static void HandleNativeLog(string message, uint hexColor)
        {
            try
            {
                if (!string.IsNullOrEmpty(message))
                    Log(message, hexColor);
            }
            catch (Exception exception)
            {
                // Never allow a managed exception to cross the reverse P/Invoke boundary.
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Sends a diagnostic record to every output enabled in the global VLC logging settings.
        /// </summary>
        public static void Log(string message)
        {
            Log(message, DefaultLogColor);
        }

        private static void Log(string message, uint rgbaColor)
        {
            if (string.IsNullOrEmpty(message))
                return;

            NotifyLogReceived(message);

            if (_writeToUnityConsole)
                Debug.Log($"<color=#{rgbaColor:X8}>{message}</color>");

            if (_mainThreadContext != null && _unityLogReceivedEvent != null)
                _mainThreadContext.Post(_unityEventCallback, message);

            FileLogWriter writer;
            lock (_lifecycleLock)
                writer = _fileWriter;

            writer?.TryWrite(message);
        }

        private static void NotifyLogReceived(string message)
        {
            Action<string> handlers;
            lock (_eventLock)
                handlers = _logReceived;

            if (handlers == null)
                return;

            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(message);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void DispatchLogToUnityEvent(object state)
        {
            try
            {
                _unityLogReceivedEvent?.Invoke((string)state);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ApplySettings(VLCLogSettings settings)
        {
            _includeLibVLCEngineLogs = settings != null && settings.includeLibVLCEngineLogs;
            _includeNativeRenderingLogs = settings != null && settings.includeNativeRenderingLogs;
            _writeToUnityConsole = settings != null && settings.writeToUnityConsole;
            _unityLogReceivedEvent = settings?.onLogReceived;

            StopFileWriter();

            if (settings == null || !settings.writeToFile || string.IsNullOrWhiteSpace(settings.logFilePath))
                return;

            try
            {
                string filePath = ResolveBaseLogFilePath(settings.logFilePath);

                var writer = new FileLogWriter(
                    filePath,
                    settings.rotationMode,
                    settings.rotationInterval,
                    Math.Max(1, settings.maxFileSizeMB),
                    Math.Max(0, settings.maxRetainedFiles));

                lock (_lifecycleLock)
                    _fileWriter = writer;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is IOException ||
                exception is NotSupportedException ||
                exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"VLC file logging is disabled because the log file could not be initialized: {exception.Message}");
            }
        }

        /// <summary>
        /// Re-reads the active settings asset so edits made on it take effect
        /// immediately, including during Play Mode.
        /// </summary>
        internal static void ReapplySettings(VLCLogSettings settings)
        {
            // Ignore assets that are not the one this session loaded, and edits
            // that arrive before Initialize has run.
            if (settings == null || !ReferenceEquals(settings, _settings))
                return;

            ApplySettings(settings);

            // Apply changes to the two independent low-level log sources immediately.
            if (ShouldCaptureLibVLCLogs())
                HookLibVLC(VLCMediaPlayer.LibVLC);
            else
                UnhookAllLibVLCInstances();

            RefreshNativeLogCallback();
        }

        private static void StopFileWriter()
        {
            FileLogWriter writer;
            lock (_lifecycleLock)
            {
                writer = _fileWriter;
                _fileWriter = null;
            }

            writer?.Dispose();
        }

        private static void RefreshNativeLogCallback()
        {
            lock (_nativeCallbackLock)
            {
                RefreshNativeLogCallbackLocked();
            }
        }

        private static void EnableAndRefreshNativeLogCallback()
        {
            lock (_nativeCallbackLock)
            {
                _nativeCallbackRefreshEnabled = true;
                RefreshNativeLogCallbackLocked();
            }
        }

        private static void DisableNativeLogCallback()
        {
            lock (_nativeCallbackLock)
            {
                _nativeCallbackRefreshEnabled = false;
                SetNativeLogCallbackLocked(null);

                // _logCallback is deliberately left assigned. The native side
                // gives up waiting for in-flight callbacks after a short
                // timeout rather than hanging the editor, so the delegate has
                // to stay rooted for the life of the domain.
            }
        }

        private static void RefreshNativeLogCallbackLocked()
        {
            LogCallback callback = _nativeCallbackRefreshEnabled && ShouldCaptureNativeLogs()
                ? _logCallback
                : null;
            SetNativeLogCallbackLocked(callback);
        }

        private static void SetNativeLogCallbackLocked(LogCallback callback)
        {
            if (callback != null && _nativeCallbackRegistered)
                return;

            if (callback == null && !_nativeCallbackRegistered)
                return;

#if UNITY_INCLUDE_TESTS
            if (NativeCallbackSetterForTests != null)
            {
                NativeCallbackSetterForTests(callback != null);
                _nativeCallbackRegistered = callback != null;
                return;
            }
#endif

            try
            {
                SetLogCallback(callback);
                _nativeCallbackRegistered = callback != null;
            }
            catch (Exception exception) when (
                exception is DllNotFoundException ||
                exception is EntryPointNotFoundException)
            {
                _nativeCallbackRegistered = false;
                Debug.LogWarning($"Native VLC Unity logging is unavailable: {exception.Message}");
            }
        }

        internal static bool ShouldCaptureNativeLogs()
        {
            if (!_includeNativeRenderingLogs)
                return false;

            bool hasFileOutput;
            lock (_lifecycleLock)
                hasFileOutput = _fileWriter != null;

            if (_writeToUnityConsole || hasFileOutput || _unityLogReceivedEvent != null)
                return true;

            lock (_eventLock)
                return _logReceived != null;
        }

        internal static bool ShouldCaptureLibVLCLogs()
        {
            return _includeLibVLCEngineLogs;
        }

        internal static void InitializeFileLoggingForTests(
            VLCLogSettings settings,
            SynchronizationContext synchronizationContext = null)
        {
            _settings = settings;
            _mainThreadContext = synchronizationContext;
            ApplySettings(settings);
        }

        internal static void ShutdownFileLoggingForTests()
        {
            StopFileWriter();
            _includeLibVLCEngineLogs = false;
            _includeNativeRenderingLogs = false;
            _writeToUnityConsole = false;
            _unityLogReceivedEvent = null;
        }

        internal static string ResolveBaseLogFilePath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return null;

            string filePath = configuredPath;
            if (!Path.IsPathRooted(filePath))
                filePath = Path.Combine(Application.persistentDataPath, filePath);

            return Path.GetFullPath(filePath);
        }

        internal static string ResolveCurrentLogFilePath(VLCLogSettings settings, DateTime timestamp)
        {
            if (settings == null)
                return null;

            string baseFilePath = ResolveBaseLogFilePath(settings.logFilePath);
            if (baseFilePath == null || settings.rotationMode != LogRotationMode.TimeInterval)
                return baseFilePath;

            string directory = Path.GetDirectoryName(baseFilePath);
            string fileName = Path.GetFileNameWithoutExtension(baseFilePath);
            string extension = Path.GetExtension(baseFilePath);
            string timeSuffix = timestamp.ToString(GetRotationTimeFormat(settings.rotationInterval));
            return Path.Combine(directory, $"{fileName}_{timeSuffix}{extension}");
        }

        private static string GetRotationTimeFormat(LogRotationInterval rotationInterval)
        {
            return rotationInterval switch
            {
                LogRotationInterval.Hourly => "yyyy-MM-dd_HH",
                LogRotationInterval.Monthly => "yyyy-MM",
                _ => "yyyy-MM-dd"
            };
        }

        private sealed class FileLogWriter : IDisposable
        {
            private readonly BlockingCollection<string> _queue =
                new(new ConcurrentQueue<string>(), FileQueueCapacity);
            private readonly string _baseFilePath;
            private readonly string _directory;
            private readonly string _fileName;
            private readonly string _extension;
            private readonly string _timeFormat;
            private readonly LogRotationMode _rotationMode;
            private readonly long _maxFileSizeBytes;
            private readonly int _maxRetainedFiles;
            private readonly Task _worker;

            private int _disposeStarted;
            private int _droppedMessageCount;

            internal FileLogWriter(
                string filePath,
                LogRotationMode rotationMode,
                LogRotationInterval rotationInterval,
                int maxFileSizeMegabytes,
                int maxRetainedFiles)
            {
                _baseFilePath = Path.GetFullPath(filePath);
                _directory = Path.GetDirectoryName(_baseFilePath);

                if (string.IsNullOrEmpty(_directory))
                    throw new ArgumentException("The log file path must include a directory.", nameof(filePath));

                _fileName = Path.GetFileNameWithoutExtension(_baseFilePath);
                _extension = Path.GetExtension(_baseFilePath);
                _rotationMode = rotationMode;
                _maxFileSizeBytes = maxFileSizeMegabytes * 1024L * 1024L;
                _maxRetainedFiles = maxRetainedFiles;
                _timeFormat = GetRotationTimeFormat(rotationInterval);

                Directory.CreateDirectory(_directory);
                InitializePreviousSessionFile();
                _worker = Task.Run(ProcessQueue);
            }

            internal void TryWrite(string message)
            {
                if (Volatile.Read(ref _disposeStarted) != 0)
                    return;

                try
                {
                    if (!_queue.TryAdd(message))
                        Interlocked.Increment(ref _droppedMessageCount);
                }
                catch (InvalidOperationException)
                {
                    // Shutdown won the race. This also covers ObjectDisposedException;
                    // the writer will flush messages already accepted.
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
                    return;

                _queue.CompleteAdding();

                try
                {
                    _worker.GetAwaiter().GetResult();
                }
                finally
                {
                    _queue.Dispose();
                }
            }

            private void ProcessQueue()
            {
                var batch = new List<string>();

                foreach (string message in _queue.GetConsumingEnumerable())
                {
                    batch.Add(message);

                    // Keep the in-memory batch bounded as well as the producer
                    // queue. Under sustained logging, draining until the queue
                    // becomes empty can otherwise grow this list indefinitely.
                    while (batch.Count < FileBatchCapacity && _queue.TryTake(out string nextMessage))
                        batch.Add(nextMessage);

                    AppendDroppedMessageNotice(batch);
                    WriteBatchSafely(batch);
                    batch.Clear();
                }

                AppendDroppedMessageNotice(batch);
                if (batch.Count > 0)
                    WriteBatchSafely(batch);
            }

            private void AppendDroppedMessageNotice(List<string> batch)
            {
                int dropped = Interlocked.Exchange(ref _droppedMessageCount, 0);
                if (dropped > 0)
                    batch.Add($"[VLC-Unity] Dropped {dropped} file log messages because the queue was full.");
            }

            private void WriteBatchSafely(List<string> batch)
            {
                try
                {
                    string targetPath = GetTargetPath();

                    using var stream = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream);

                    foreach (string message in batch)
                        writer.WriteLine(message);

                    // Flush to the OS, not through to the disk platter. This is
                    // a diagnostic log; forcing FlushFileBuffers on every batch
                    // costs far more than a lost tail is worth.
                    writer.Flush();
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is IOException ||
                    exception is NotSupportedException ||
                    exception is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"Failed to write VLC log messages: {exception.Message}");
                }
            }

            private string GetTargetPath()
            {
                if (_rotationMode == LogRotationMode.TimeInterval)
                {
                    string timeSuffix = DateTime.Now.ToString(_timeFormat);
                    return Path.Combine(_directory, $"{_fileName}_{timeSuffix}{_extension}");
                }

                if (_rotationMode == LogRotationMode.FileSize &&
                    File.Exists(_baseFilePath) &&
                    new FileInfo(_baseFilePath).Length >= _maxFileSizeBytes)
                {
                    RollSizeBasedFiles();
                }

                return _baseFilePath;
            }

            private void InitializePreviousSessionFile()
            {
                if (_rotationMode != LogRotationMode.PreviousSession || !File.Exists(_baseFilePath))
                    return;

                string previousPath = Path.Combine(_directory, $"{_fileName}-prev{_extension}");
                ReplaceOrMove(_baseFilePath, previousPath);
            }

            private void RollSizeBasedFiles()
            {
                if (_maxRetainedFiles == 0)
                {
                    File.Delete(_baseFilePath);
                    return;
                }

                for (int index = _maxRetainedFiles - 1; index >= 1; index--)
                {
                    string oldPath = Path.Combine(_directory, $"{_fileName}_{index}{_extension}");
                    string newPath = Path.Combine(_directory, $"{_fileName}_{index + 1}{_extension}");

                    if (File.Exists(oldPath))
                        ReplaceOrMove(oldPath, newPath);
                }

                string firstRolledPath = Path.Combine(_directory, $"{_fileName}_1{_extension}");
                ReplaceOrMove(_baseFilePath, firstRolledPath);
            }

            private static void ReplaceOrMove(string sourcePath, string destinationPath)
            {
                if (!File.Exists(destinationPath))
                {
                    File.Move(sourcePath, destinationPath);
                    return;
                }

                try
                {
                    File.Replace(sourcePath, destinationPath, null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(destinationPath);
                    File.Move(sourcePath, destinationPath);
                }
            }
        }
    }
}
