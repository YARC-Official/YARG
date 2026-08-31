#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace YARG.Editor.YargAudio
{
    public static class YargAudioPackageBuilder
    {
        private const string WORKFLOW_FILE = "native-audio.yml";
        private const int POLL_INTERVAL_MS = 3000;
        private const int UI_POLL_INTERVAL_MS = 100;
        private const int TIMEOUT_MINUTES = 45;

        private static readonly (string ArtifactName, string BinaryName, string TargetDirectory)[] PLUGINS =
        {
            ("yarg-audio-windows-x64", "yarg_audio.dll", "Assets/Plugins/YargAudio/Windows/x86_64"),
            ("yarg-audio-linux-x64", "libyarg_audio.so", "Assets/Plugins/YargAudio/Linux/x86_64"),
            ("yarg-audio-macos-universal", "libyarg_audio.dylib", "Assets/Plugins/YargAudio/Mac"),
        };

        public static async void RebuildAllPlatforms()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            if (!CheckPrerequisites(projectRoot, out string branchRef, out string? repository))
            {
                return;
            }

            var tempDownloadDirectory = Path.Combine(
                Path.GetTempPath(),
                $"yarg-audio-package-{Guid.NewGuid():N}");

            try
            {
                await ExecuteAllPlatformsBuildAsync(projectRoot, branchRef, repository, tempDownloadDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[YargAudio Package] Unexpected error: {exception.Message}");
                EditorUtility.DisplayDialog("Native Audio Build Error", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (Directory.Exists(tempDownloadDirectory))
                {
                    try
                    {
                        Directory.Delete(tempDownloadDirectory, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        private static bool CheckPrerequisites(string projectRoot, out string branchRef, out string? repository)
        {
            branchRef = string.Empty;
            repository = null;

            int ghCheck = RunProcess("gh", "--version", projectRoot, out string _, out string _);
            if (ghCheck != 0)
            {
                bool openSite = EditorUtility.DisplayDialog(
                    "GitHub CLI (gh) Required",
                    "Building for all platforms requires the GitHub CLI (gh) to trigger GitHub Actions remote runners.\n\n" +
                    "How to install:\n" +
                    "• Windows: winget install GitHub.cli\n" +
                    "• macOS: brew install gh\n" +
                    "• Linux: Install via your package manager (e.g., apt, dnf, pacman)\n\n" +
                    "After installing, restart Unity and run 'gh auth login' in your terminal.",
                    "Download gh",
                    "Cancel");

                if (openSite)
                {
                    Application.OpenURL("https://cli.github.com/");
                }

                return false;
            }

            int authCheck = RunProcess("gh", "auth status", projectRoot, out string _, out string authError);
            if (authCheck != 0)
            {
                EditorUtility.DisplayDialog(
                    "GitHub Authentication Required",
                    "GitHub CLI is installed but not authenticated.\n\n" +
                    "Please run 'gh auth login' in your terminal and complete the browser login prompt.\n\n" +
                    authError.Trim(),
                    "OK");
                return false;
            }

            RunProcess("git", "branch --show-current", projectRoot, out string branchName, out string _);
            branchName = branchName.Trim();

            if (string.IsNullOrWhiteSpace(branchName))
            {
                RunProcess("git", "rev-parse HEAD", projectRoot, out string commitSha, out string _);
                branchName = commitSha.Trim();
            }

            if (string.IsNullOrWhiteSpace(branchName))
            {
                EditorUtility.DisplayDialog(
                    "Git Branch Not Found",
                    "Could not determine the current Git branch or commit to build on GitHub Actions.",
                    "OK");
                return false;
            }

            repository = GetRemoteRepository(projectRoot, branchName, out string? remoteName);

            int trackingCheck = RunProcess("git", "rev-parse --abbrev-ref @{u}", projectRoot, out string _, out string _);
            if (trackingCheck != 0)
            {
                string checkArg = !string.IsNullOrEmpty(remoteName)
                    ? $"ls-remote --heads {remoteName} {branchName}"
                    : $"ls-remote --heads origin {branchName}";

                int remoteCheck = RunProcess("git", checkArg, projectRoot, out string remoteBranchMatch, out string _);
                if (remoteCheck != 0 || string.IsNullOrWhiteSpace(remoteBranchMatch))
                {
                    EditorUtility.DisplayDialog(
                        "Branch Not Pushed",
                        $"Branch '{branchName}' has not been pushed to GitHub.\n\n" +
                        "GitHub Actions can only build branches that exist on the remote repository.\n\n" +
                        "Please push your branch to GitHub before rebuilding all platforms.",
                        "OK");
                    return false;
                }
            }
            else
            {
                RunProcess("git", "log @{u}..HEAD --oneline", projectRoot, out string unpushedCommits, out string _);
                if (!string.IsNullOrWhiteSpace(unpushedCommits))
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "Unpushed Commits Detected",
                        $"Branch '{branchName}' has local commits that have not yet been pushed to GitHub.\n\n" +
                        "GitHub Actions only builds commits available on the remote repository.\n\n" +
                        "Do you want to proceed using the latest pushed commit on remote?",
                        "Proceed Anyway",
                        "Cancel");

                    if (!proceed)
                    {
                        return false;
                    }
                }
            }

            branchRef = branchName;
            return true;
        }

        private static async Task ExecuteAllPlatformsBuildAsync(
            string projectRoot,
            string branchRef,
            string? repository,
            string tempDownloadDirectory)
        {
            const string TITLE = "Rebuilding Native Audio (All Platforms)";

            var dispatchTask = RunGhAsync(
                $"workflow run {WORKFLOW_FILE} --ref {branchRef}",
                repository,
                projectRoot);

            if (await WaitWithProgressAsync(TITLE, $"Dispatching workflow on branch '{branchRef}'...", 0.05f, dispatchTask))
            {
                Debug.Log("[YargAudio Package] Build canceled by user.");
                return;
            }

            var (dispatchExit, dispatchOut, dispatchErr) = await dispatchTask;
            if (dispatchExit != 0)
            {
                string error = string.IsNullOrWhiteSpace(dispatchErr) ? dispatchOut : dispatchErr;
                EditorUtility.DisplayDialog(
                    "Workflow Dispatch Failed",
                    $"Failed to trigger GitHub Actions workflow:\n{error}",
                    "OK");
                return;
            }

            var dispatchStarted = DateTimeOffset.UtcNow;
            long? runId = null;
            var runUrl = string.Empty;
            var deadline = DateTimeOffset.UtcNow.AddMinutes(TIMEOUT_MINUTES);
            var lastListPoll = DateTimeOffset.MinValue;
            Task<(int ExitCode, string Stdout, string Stderr)>? pendingListTask = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    TITLE, "Waiting for GitHub Actions run to start...", 0.10f))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("[YargAudio Package] Build canceled by user.");
                    return;
                }

                if (pendingListTask == null && (DateTimeOffset.UtcNow - lastListPoll).TotalMilliseconds >= POLL_INTERVAL_MS)
                {
                    lastListPoll = DateTimeOffset.UtcNow;
                    pendingListTask = RunGhAsync(
                        $"run list --workflow {WORKFLOW_FILE} --limit 20 --json databaseId,headBranch,headSha,createdAt,url,event",
                        repository,
                        projectRoot);
                }

                if (pendingListTask != null && pendingListTask.IsCompleted)
                {
                    var (listExit, listOut, _) = await pendingListTask;
                    pendingListTask = null;

                    if (listExit == 0 && TryParseDispatchedRun(listOut, branchRef, dispatchStarted, out long foundId, out string foundUrl))
                    {
                        runId = foundId;
                        runUrl = foundUrl;
                        break;
                    }
                }

                await Task.Delay(UI_POLL_INTERVAL_MS);
            }

            if (!runId.HasValue)
            {
                EditorUtility.DisplayDialog(
                    "Timeout",
                    "Timed out waiting for GitHub Actions to register the dispatched workflow run.",
                    "OK");
                return;
            }

            Debug.Log($"[YargAudio Package] Tracking workflow run: {runUrl}");

            var runStart = DateTimeOffset.UtcNow;
            var lastViewPoll = DateTimeOffset.MinValue;
            Task<(int ExitCode, string Stdout, string Stderr)>? pendingViewTask = null;
            var jobsSummary = "Waiting for runner assignment...";
            float jobsProgress = 0f;
            var workflowConclusion = string.Empty;

            while (DateTimeOffset.UtcNow < deadline)
            {
                var elapsed = DateTimeOffset.UtcNow - runStart;
                var elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                float totalProgress = 0.15f + (jobsProgress * 0.70f);
                var statusLine = $"{jobsSummary} ({elapsedTime} elapsed)";

                if (EditorUtility.DisplayCancelableProgressBar(TITLE, statusLine, totalProgress))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("[YargAudio Package] Build canceled by user.");
                    _ = Task.Run(() => RunGh($"run cancel {runId.Value}", repository, projectRoot, out _, out _));
                    return;
                }

                if (pendingViewTask == null && (DateTimeOffset.UtcNow - lastViewPoll).TotalMilliseconds >= POLL_INTERVAL_MS)
                {
                    lastViewPoll = DateTimeOffset.UtcNow;
                    pendingViewTask = RunGhAsync(
                        $"run view {runId.Value} --json status,conclusion,jobs",
                        repository,
                        projectRoot);
                }

                if (pendingViewTask != null && pendingViewTask.IsCompleted)
                {
                    var (viewExit, viewOut, _) = await pendingViewTask;
                    pendingViewTask = null;

                    if (viewExit == 0 && TryParseRunStatus(viewOut, out string status, out string conclusion, out string parsedSummary, out float parsedProgress))
                    {
                        jobsSummary = parsedSummary;
                        jobsProgress = parsedProgress;

                        if (status == "completed")
                        {
                            workflowConclusion = conclusion;
                            break;
                        }
                    }
                }

                await Task.Delay(UI_POLL_INTERVAL_MS);
            }

            if (workflowConclusion != "success")
            {
                bool openUrl = EditorUtility.DisplayDialog(
                    "Native Audio Build Failed",
                    $"GitHub Actions run completed with status '{workflowConclusion}'.\n\nRun URL: {runUrl}",
                    "Open Run URL",
                    "OK");

                if (openUrl)
                {
                    Application.OpenURL(runUrl);
                }

                return;
            }

            Directory.CreateDirectory(tempDownloadDirectory);
            var downloadTask = RunGhAsync(
                $"run download {runId.Value} --pattern \"yarg-audio-*\" --dir \"{tempDownloadDirectory}\"",
                repository,
                projectRoot);

            if (await WaitWithProgressAsync(TITLE, "Downloading multi-platform artifacts...", 0.88f, downloadTask))
            {
                Debug.Log("[YargAudio Package] Build canceled by user.");
                return;
            }

            var (downloadExit, downloadOut, downloadErr) = await downloadTask;
            if (downloadExit != 0)
            {
                string error = string.IsNullOrWhiteSpace(downloadErr) ? downloadOut : downloadErr;
                EditorUtility.DisplayDialog(
                    "Artifact Download Failed",
                    $"Failed to download artifacts from GitHub Actions:\n{error}",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayCancelableProgressBar(
                TITLE, "Installing native plugins...", 0.95f))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            InstallDownloadedPlugins(projectRoot, tempDownloadDirectory);

            EditorUtility.ClearProgressBar();
            YARG.Audio.BASS.Native.YargAudioBindings.Reload();
            AssetDatabase.Refresh();

            Debug.Log("[YargAudio Package] Successfully rebuilt and updated native audio plugins for all platforms.");
            EditorApplication.delayCall += () =>
            {
                EditorUtility.DisplayDialog(
                    "Rebuild Complete",
                    "Successfully updated native audio plugins for Windows, Linux, and macOS.",
                    "OK");
            };
        }

        private static async Task<bool> WaitWithProgressAsync(
            string title,
            string status,
            float progress,
            Task backgroundTask)
        {
            while (!backgroundTask.IsCompleted)
            {
                if (EditorUtility.DisplayCancelableProgressBar(title, status, progress))
                {
                    EditorUtility.ClearProgressBar();
                    return true;
                }

                await Task.Delay(UI_POLL_INTERVAL_MS);
            }

            return false;
        }

        private static bool TryParseDispatchedRun(
            string json,
            string branchRef,
            DateTimeOffset dispatchStarted,
            out long runId,
            out string runUrl)
        {
            runId = 0;
            runUrl = string.Empty;

            try
            {
                var runs = JArray.Parse(json);
                var earliest = dispatchStarted.AddSeconds(-30);

                foreach (var token in runs)
                {
                    if (token is not JObject element)
                    {
                        continue;
                    }

                    var eventType = (string?) element["event"] ?? string.Empty;
                    var createdAtStr = (string?) element["createdAt"] ?? string.Empty;

                    if (DateTimeOffset.TryParse(createdAtStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt) &&
                        eventType == "workflow_dispatch" &&
                        createdAt >= earliest)
                    {
                        var headBranch = (string?) element["headBranch"] ?? string.Empty;
                        var headSha = (string?) element["headSha"] ?? string.Empty;

                        if (string.Equals(headBranch, branchRef, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(headSha, branchRef, StringComparison.OrdinalIgnoreCase) ||
                            branchRef.EndsWith(headBranch, StringComparison.OrdinalIgnoreCase))
                        {
                            runId = (long?) element["databaseId"] ?? 0;
                            runUrl = (string?) element["url"] ?? string.Empty;
                            return runId > 0;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool TryParseRunStatus(
            string json,
            out string status,
            out string conclusion,
            out string jobsSummary,
            out float jobsProgress)
        {
            status = string.Empty;
            conclusion = string.Empty;
            jobsSummary = string.Empty;
            jobsProgress = 0f;

            try
            {
                var root = JObject.Parse(json);

                status = (string?) root["status"] ?? string.Empty;
                conclusion = (string?) root["conclusion"] ?? string.Empty;

                var summaries = new List<string>();
                int totalJobs = 0;
                int completedJobs = 0;

                if (root["jobs"] is JArray jobs)
                {
                    foreach (var jobToken in jobs)
                    {
                        if (jobToken is not JObject job)
                        {
                            continue;
                        }

                        totalJobs++;
                        var name = (string?) job["name"] ?? string.Empty;
                        var jobStatus = (string?) job["status"] ?? string.Empty;
                        var jobConclusion = (string?) job["conclusion"] ?? string.Empty;

                        var shortName = name.Contains("Windows") ? "Win" :
                            name.Contains("Linux") ? "Linux" :
                            name.Contains("macOS") ? "Mac" : name;

                        var displayStatus = jobStatus == "completed"
                            ? (!string.IsNullOrEmpty(jobConclusion) ? jobConclusion : "done")
                            : jobStatus;

                        if (jobStatus == "completed")
                        {
                            completedJobs++;
                        }

                        summaries.Add($"{shortName}: {displayStatus}");
                    }
                }

                jobsProgress = totalJobs > 0 ? (float) completedJobs / totalJobs : 0f;
                jobsSummary = summaries.Count > 0 ? string.Join(" | ", summaries) : $"Status: {status}";
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void InstallDownloadedPlugins(string projectRoot, string downloadDirectory)
        {
            foreach (var (artifactName, binaryName, targetDirectory) in PLUGINS)
            {
                var artifactPath = Path.Combine(downloadDirectory, artifactName);
                IEnumerable<string> searchRoots = Directory.Exists(artifactPath)
                    ? new[] { artifactPath }
                    : new[] { downloadDirectory };

                var foundBinaries = searchRoots
                    .SelectMany(root => Directory.Exists(root)
                        ? Directory.EnumerateFiles(root, binaryName, SearchOption.AllDirectories)
                        : Array.Empty<string>())
                    .ToArray();

                if (foundBinaries.Length == 0)
                {
                    throw new FileNotFoundException($"Artifact '{artifactName}' did not contain '{binaryName}'.");
                }

                var sourceBinary = foundBinaries[0];
                var sourceMetadata = sourceBinary + ".meta";

                var destinationFolder = Path.Combine(projectRoot, targetDirectory);
                Directory.CreateDirectory(destinationFolder);

                var destBinary = Path.Combine(destinationFolder, binaryName);
                var destMetadata = destBinary + ".meta";

                File.Copy(sourceBinary, destBinary, overwrite: true);
                if (File.Exists(sourceMetadata))
                {
                    File.Copy(sourceMetadata, destMetadata, overwrite: true);
                }
            }
        }

        private static string? GetRemoteRepository(string projectRoot, string branchName, out string? remoteName)
        {
            remoteName = GetConfiguredRemoteName(projectRoot, branchName);
            if (string.IsNullOrEmpty(remoteName))
            {
                return null;
            }

            int exit = RunProcess("git", $"remote get-url {remoteName}", projectRoot, out string remoteUrl, out _);
            if (exit != 0 || string.IsNullOrWhiteSpace(remoteUrl))
            {
                return null;
            }

            return remoteUrl.Trim().ParseRepositorySlug();
        }

        private static string? GetConfiguredRemoteName(string projectRoot, string branchName)
        {
            int branchRemoteExit = RunProcess("git", $"config --get branch.{branchName}.remote", projectRoot, out string branchRemote, out _);
            if (branchRemoteExit == 0 && !string.IsNullOrWhiteSpace(branchRemote))
            {
                return branchRemote.Trim();
            }

            int originExit = RunProcess("git", "remote get-url origin", projectRoot, out _, out _);
            if (originExit == 0)
            {
                return "origin";
            }

            int listExit = RunProcess("git", "remote", projectRoot, out string allRemotes, out _);
            if (listExit == 0 && !string.IsNullOrWhiteSpace(allRemotes))
            {
                var remotes = allRemotes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (remotes.Length > 0)
                {
                    return remotes[0].Trim();
                }
            }

            return null;
        }

        private static Task<(int ExitCode, string Stdout, string Stderr)> RunGhAsync(
            string arguments,
            string? repository,
            string workingDirectory)
        {
            var repoFlag = string.IsNullOrEmpty(repository) ? string.Empty : $"-R {repository} ";
            return RunProcessAsync("gh", $"{repoFlag}{arguments}", workingDirectory);
        }

        private static int RunGh(
            string arguments,
            string? repository,
            string workingDirectory,
            out string stdout,
            out string stderr)
        {
            var repoFlag = string.IsNullOrEmpty(repository) ? string.Empty : $"-R {repository} ";
            return RunProcess("gh", $"{repoFlag}{arguments}", workingDirectory, out stdout, out stderr);
        }

        private static Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(
            string filename,
            string arguments,
            string workingDirectory) =>
            Task.Run(() =>
            {
                int exit = RunProcess(filename, arguments, workingDirectory, out string stdout, out string stderr);
                return (exit, stdout, stderr);
            });

        private static int RunProcess(
            string filename,
            string arguments,
            string workingDirectory,
            out string stdout,
            out string stderr)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.EnvironmentVariables["GITHUB_TOKEN"] = string.Empty;

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                stdout = string.Empty;
                stderr = exception.Message;
                return -1;
            }
        }
    }

    internal static class YargAudioPackageBuilderExtensions
    {
        internal static string? ParseRepositorySlug(this string remoteUrl)
        {
            var cleanedUrl = remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? remoteUrl.Substring(0, remoteUrl.Length - 4)
                : remoteUrl;

            int lastSlash = cleanedUrl.LastIndexOfAny(new[] { '/', ':' });
            if (lastSlash > 0)
            {
                int prevSlash = cleanedUrl.LastIndexOfAny(new[] { '/', ':' }, lastSlash - 1);
                if (prevSlash >= 0)
                {
                    return cleanedUrl.Substring(prevSlash + 1);
                }
            }

            return null;
        }
    }
}
