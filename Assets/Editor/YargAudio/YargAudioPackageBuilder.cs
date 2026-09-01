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
        private const string BUILD_TITLE = "Rebuilding Native Audio (All Platforms)";

        private static readonly (string ArtifactName, string BinaryName, string TargetDirectory)[] PLUGINS =
        {
            ("yarg-audio-windows-x64", "yarg_audio.dll", "Assets/Plugins/YargAudio/Windows/x86_64"),
            ("yarg-audio-linux-x64", "libyarg_audio.so", "Assets/Plugins/YargAudio/Linux/x86_64"),
            ("yarg-audio-macos-universal", "libyarg_audio.dylib", "Assets/Plugins/YargAudio/Mac"),
        };

        public static async void RebuildAllPlatforms()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var request = CreateBuildRequest(projectRoot);
            if (request == null)
            {
                return;
            }

            var downloadDirectory = Path.Combine(
                Path.GetTempPath(),
                $"yarg-audio-package-{Guid.NewGuid():N}");

            try
            {
                await BuildAllPlatformsAsync(request, downloadDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[YargAudio Package] Unexpected error: {exception.Message}");
                EditorUtility.DisplayDialog("Native Audio Build Error", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (Directory.Exists(downloadDirectory))
                {
                    try
                    {
                        Directory.Delete(downloadDirectory, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        private static BuildRequest? CreateBuildRequest(string projectRoot)
        {
            if (!IsGitHubCliReady(projectRoot))
            {
                return null;
            }

            var branchRef = GetBranchRef(projectRoot);
            if (string.IsNullOrWhiteSpace(branchRef))
            {
                EditorUtility.DisplayDialog(
                    "Git Branch Not Found",
                    "Could not determine the current Git branch or commit to build on GitHub Actions.",
                    "OK");
                return null;
            }

            var repository = GetRemoteRepository(projectRoot, branchRef, out var remoteName);
            return ConfirmRemoteBranch(projectRoot, branchRef, remoteName)
                ? new BuildRequest(
                    projectRoot: projectRoot,
                    branchRef: branchRef,
                    repository: repository)
                : null;
        }

        private static bool IsGitHubCliReady(string projectRoot)
        {
            var versionResult = RunProcess(
                filename: "gh",
                arguments: "--version",
                workingDirectory: projectRoot);
            if (!versionResult.Succeeded)
            {
                var openSite = EditorUtility.DisplayDialog(
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

            var authResult = RunProcess(
                filename: "gh",
                arguments: "auth status",
                workingDirectory: projectRoot);
            if (authResult.Succeeded)
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "GitHub Authentication Required",
                "GitHub CLI is installed but not authenticated.\n\n" +
                "Please run 'gh auth login' in your terminal and complete the browser login prompt.\n\n" +
                authResult.Stderr.Trim(),
                "OK");
            return false;
        }

        private static string GetBranchRef(string projectRoot)
        {
            var branchResult = RunProcess(
                filename: "git",
                arguments: "branch --show-current",
                workingDirectory: projectRoot);
            var branchRef = branchResult.Stdout.Trim();
            if (!string.IsNullOrWhiteSpace(branchRef))
            {
                return branchRef;
            }

            return RunProcess(
                filename: "git",
                arguments: "rev-parse HEAD",
                workingDirectory: projectRoot).Stdout.Trim();
        }

        private static bool ConfirmRemoteBranch(string projectRoot, string branchRef, string? remoteName)
        {
            var trackingResult = RunProcess(
                filename: "git",
                arguments: "rev-parse --abbrev-ref @{u}",
                workingDirectory: projectRoot);
            if (!trackingResult.Succeeded)
            {
                var remote = string.IsNullOrEmpty(remoteName) ? "origin" : remoteName;
                var remoteResult = RunProcess(
                    filename: "git",
                    arguments: $"ls-remote --heads {remote} {branchRef}",
                    workingDirectory: projectRoot);

                if (!remoteResult.Succeeded || string.IsNullOrWhiteSpace(remoteResult.Stdout))
                {
                    EditorUtility.DisplayDialog(
                        "Branch Not Pushed",
                        $"Branch '{branchRef}' has not been pushed to GitHub.\n\n" +
                        "GitHub Actions can only build branches that exist on the remote repository.\n\n" +
                        "Please push your branch to GitHub before rebuilding all platforms.",
                        "OK");
                    return false;
                }

                return true;
            }

            var unpushedResult = RunProcess(
                filename: "git",
                arguments: "log @{u}..HEAD --oneline",
                workingDirectory: projectRoot);
            if (string.IsNullOrWhiteSpace(unpushedResult.Stdout))
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "Unpushed Commits Detected",
                $"Branch '{branchRef}' has local commits that have not yet been pushed to GitHub.\n\n" +
                "GitHub Actions only builds commits available on the remote repository.\n\n" +
                "Do you want to proceed using the latest pushed commit on remote?",
                "Proceed Anyway",
                "Cancel");
        }

        private static async Task BuildAllPlatformsAsync(
            BuildRequest request,
            string downloadDirectory)
        {
            var dispatchStarted = await DispatchWorkflowAsync(request);
            if (!dispatchStarted.HasValue)
            {
                return;
            }

            var deadline = DateTimeOffset.UtcNow.AddMinutes(TIMEOUT_MINUTES);
            var run = await FindDispatchedRunAsync(request, dispatchStarted.Value, deadline);
            if (run == null || !await WaitForWorkflowAsync(request, run, deadline))
            {
                return;
            }

            if (!await DownloadArtifactsAsync(request, run.Id, downloadDirectory))
            {
                return;
            }

            if (EditorUtility.DisplayCancelableProgressBar(BUILD_TITLE, "Installing native plugins...", 0.95f))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            InstallDownloadedPlugins(request.ProjectRoot, downloadDirectory);

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

        private static async Task<DateTimeOffset?> DispatchWorkflowAsync(BuildRequest request)
        {
            var dispatchTask = RunGhAsync(
                arguments: $"workflow run {WORKFLOW_FILE} --ref {request.BranchRef}",
                repository: request.Repository,
                workingDirectory: request.ProjectRoot);

            var status = $"Dispatching workflow on branch '{request.BranchRef}'...";
            if (await WaitWithProgressAsync(BUILD_TITLE, status, 0.05f, dispatchTask))
            {
                Debug.Log("[YargAudio Package] Build canceled by user.");
                return null;
            }

            var result = await dispatchTask;
            if (!result.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "Workflow Dispatch Failed",
                    $"Failed to trigger GitHub Actions workflow:\n{result.Error}",
                    "OK");
                return null;
            }

            return DateTimeOffset.UtcNow;
        }

        private static async Task<WorkflowRun?> FindDispatchedRunAsync(
            BuildRequest request,
            DateTimeOffset dispatchStarted,
            DateTimeOffset deadline)
        {
            var lastListPoll = DateTimeOffset.MinValue;
            Task<ProcessResult>? pendingListTask = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    BUILD_TITLE, "Waiting for GitHub Actions run to start...", 0.10f))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("[YargAudio Package] Build canceled by user.");
                    return null;
                }

                if (pendingListTask == null && (DateTimeOffset.UtcNow - lastListPoll).TotalMilliseconds >= POLL_INTERVAL_MS)
                {
                    lastListPoll = DateTimeOffset.UtcNow;
                    pendingListTask = RunGhAsync(
                        arguments: $"run list --workflow {WORKFLOW_FILE} --limit 20 --json databaseId,headBranch,headSha,createdAt,url,event",
                        repository: request.Repository,
                        workingDirectory: request.ProjectRoot);
                }

                if (pendingListTask != null && pendingListTask.IsCompleted)
                {
                    var result = await pendingListTask;
                    pendingListTask = null;

                    if (result.Succeeded)
                    {
                        var run = ParseDispatchedRun(result.Stdout, request.BranchRef, dispatchStarted);
                        if (run != null)
                        {
                            return run;
                        }
                    }
                }

                await Task.Delay(UI_POLL_INTERVAL_MS);
            }

            EditorUtility.DisplayDialog(
                "Timeout",
                "Timed out waiting for GitHub Actions to register the dispatched workflow run.",
                "OK");
            return null;
        }

        private static async Task<bool> WaitForWorkflowAsync(
            BuildRequest request,
            WorkflowRun run,
            DateTimeOffset deadline)
        {
            Debug.Log($"[YargAudio Package] Tracking workflow run: {run.Url}");

            var runStart = DateTimeOffset.UtcNow;
            var lastViewPoll = DateTimeOffset.MinValue;
            Task<ProcessResult>? pendingViewTask = null;
            var jobsSummary = "Waiting for runner assignment...";
            var jobsProgress = 0f;
            var workflowConclusion = string.Empty;

            while (DateTimeOffset.UtcNow < deadline)
            {
                var elapsed = DateTimeOffset.UtcNow - runStart;
                var elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                var totalProgress = 0.15f + (jobsProgress * 0.70f);
                var statusLine = $"{jobsSummary} ({elapsedTime} elapsed)";

                if (EditorUtility.DisplayCancelableProgressBar(BUILD_TITLE, statusLine, totalProgress))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("[YargAudio Package] Build canceled by user.");
                    _ = RunGhAsync(
                        arguments: $"run cancel {run.Id}",
                        repository: request.Repository,
                        workingDirectory: request.ProjectRoot);
                    return false;
                }

                if (pendingViewTask == null && (DateTimeOffset.UtcNow - lastViewPoll).TotalMilliseconds >= POLL_INTERVAL_MS)
                {
                    lastViewPoll = DateTimeOffset.UtcNow;
                    pendingViewTask = RunGhAsync(
                        arguments: $"run view {run.Id} --json status,conclusion,jobs",
                        repository: request.Repository,
                        workingDirectory: request.ProjectRoot);
                }

                if (pendingViewTask != null && pendingViewTask.IsCompleted)
                {
                    var result = await pendingViewTask;
                    pendingViewTask = null;

                    if (result.Succeeded)
                    {
                        var workflow = ParseRunStatus(result.Stdout);
                        if (workflow != null)
                        {
                            jobsSummary = workflow.JobsSummary;
                            jobsProgress = workflow.JobsProgress;

                            if (workflow.Status == "completed")
                            {
                                workflowConclusion = workflow.Conclusion;
                                break;
                            }
                        }
                    }
                }

                await Task.Delay(UI_POLL_INTERVAL_MS);
            }

            if (workflowConclusion != "success")
            {
                var openUrl = EditorUtility.DisplayDialog(
                    "Native Audio Build Failed",
                    $"GitHub Actions run completed with status '{workflowConclusion}'.\n\nRun URL: {run.Url}",
                    "Open Run URL",
                    "OK");

                if (openUrl)
                {
                    Application.OpenURL(run.Url);
                }

                return false;
            }

            return true;
        }

        private static async Task<bool> DownloadArtifactsAsync(
            BuildRequest request,
            long runId,
            string downloadDirectory)
        {
            Directory.CreateDirectory(downloadDirectory);
            var downloadTask = RunGhAsync(
                arguments: $"run download {runId} --pattern \"yarg-audio-*\" --dir \"{downloadDirectory}\"",
                repository: request.Repository,
                workingDirectory: request.ProjectRoot);

            if (await WaitWithProgressAsync(
                BUILD_TITLE,
                "Downloading multi-platform artifacts...",
                0.88f,
                downloadTask))
            {
                Debug.Log("[YargAudio Package] Build canceled by user.");
                return false;
            }

            var result = await downloadTask;
            if (!result.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "Artifact Download Failed",
                    $"Failed to download artifacts from GitHub Actions:\n{result.Error}",
                    "OK");
                return false;
            }

            return true;
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

        private static WorkflowRun? ParseDispatchedRun(
            string json,
            string branchRef,
            DateTimeOffset dispatchStarted)
        {
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

                    if (element.MatchesDispatchedRun(branchRef, earliest))
                    {
                        var runId = (long?) element["databaseId"] ?? 0;
                        if (runId > 0)
                        {
                            var runUrl = (string?) element["url"] ?? string.Empty;
                            return new WorkflowRun(runId, runUrl);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static WorkflowStatus? ParseRunStatus(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var status = (string?) root["status"] ?? string.Empty;
                var conclusion = (string?) root["conclusion"] ?? string.Empty;

                var summaries = new List<string>();
                var totalJobs = 0;
                var completedJobs = 0;

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

                        var shortName = name;
                        if (name.Contains("Windows"))
                        {
                            shortName = "Win";
                        }
                        else if (name.Contains("Linux"))
                        {
                            shortName = "Linux";
                        }
                        else if (name.Contains("macOS"))
                        {
                            shortName = "Mac";
                        }

                        var displayStatus = jobStatus;
                        if (jobStatus == "completed")
                        {
                            displayStatus = string.IsNullOrEmpty(jobConclusion) ? "done" : jobConclusion;
                        }

                        if (jobStatus == "completed")
                        {
                            completedJobs++;
                        }

                        summaries.Add($"{shortName}: {displayStatus}");
                    }
                }

                var jobsProgress = totalJobs > 0 ? (float) completedJobs / totalJobs : 0f;
                var jobsSummary = summaries.Count > 0 ? string.Join(" | ", summaries) : $"Status: {status}";
                return new WorkflowStatus(
                    status: status,
                    conclusion: conclusion,
                    jobsSummary: jobsSummary,
                    jobsProgress: jobsProgress);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void InstallDownloadedPlugins(string projectRoot, string downloadDirectory)
        {
            foreach (var (artifactName, binaryName, targetDirectory) in PLUGINS)
            {
                var artifactPath = Path.Combine(downloadDirectory, artifactName);
                var searchRoots = Directory.Exists(artifactPath)
                    ? new[] { artifactPath }
                    : new[] { downloadDirectory };

                var sourceBinary = searchRoots
                    .SelectMany(root => Directory.Exists(root)
                        ? Directory.EnumerateFiles(root, binaryName, SearchOption.AllDirectories)
                        : Array.Empty<string>())
                    .FirstOrDefault();

                if (sourceBinary == null)
                {
                    throw new FileNotFoundException($"Artifact '{artifactName}' did not contain '{binaryName}'.");
                }

                var sourceMetadata = sourceBinary + ".meta";

                var destinationFolder = Path.Combine(projectRoot, targetDirectory);
                Directory.CreateDirectory(destinationFolder);

                var destinationBinary = Path.Combine(destinationFolder, binaryName);
                var destinationMetadata = destinationBinary + ".meta";

                File.Copy(sourceBinary, destinationBinary, overwrite: true);
                if (File.Exists(sourceMetadata))
                {
                    File.Copy(sourceMetadata, destinationMetadata, overwrite: true);
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

            var result = RunProcess(
                filename: "git",
                arguments: $"remote get-url {remoteName}",
                workingDirectory: projectRoot);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Stdout))
            {
                return null;
            }

            return result.Stdout.Trim().ParseRepositorySlug();
        }

        private static string? GetConfiguredRemoteName(string projectRoot, string branchName)
        {
            var branchRemoteResult = RunProcess(
                filename: "git",
                arguments: $"config --get branch.{branchName}.remote",
                workingDirectory: projectRoot);
            if (branchRemoteResult.Succeeded && !string.IsNullOrWhiteSpace(branchRemoteResult.Stdout))
            {
                return branchRemoteResult.Stdout.Trim();
            }

            if (RunProcess(
                    filename: "git",
                    arguments: "remote get-url origin",
                    workingDirectory: projectRoot).Succeeded)
            {
                return "origin";
            }

            var remotesResult = RunProcess(
                filename: "git",
                arguments: "remote",
                workingDirectory: projectRoot);
            if (remotesResult.Succeeded && !string.IsNullOrWhiteSpace(remotesResult.Stdout))
            {
                var remotes = remotesResult.Stdout.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (remotes.Length > 0)
                {
                    return remotes[0].Trim();
                }
            }

            return null;
        }

        private static Task<ProcessResult> RunGhAsync(
            string arguments,
            string? repository,
            string workingDirectory)
        {
            var repoFlag = string.IsNullOrEmpty(repository) ? string.Empty : $"-R {repository} ";
            return RunProcessAsync(
                filename: "gh",
                arguments: $"{repoFlag}{arguments}",
                workingDirectory: workingDirectory);
        }

        private static Task<ProcessResult> RunProcessAsync(
            string filename,
            string arguments,
            string workingDirectory) =>
            Task.Run(() => RunProcess(
                filename: filename,
                arguments: arguments,
                workingDirectory: workingDirectory));

        private static ProcessResult RunProcess(
            string filename,
            string arguments,
            string workingDirectory)
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
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(
                    exitCode: process.ExitCode,
                    stdout: stdout,
                    stderr: stderr);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                return new ProcessResult(
                    exitCode: -1,
                    stdout: string.Empty,
                    stderr: exception.Message);
            }
        }

        private sealed class BuildRequest
        {
            public string ProjectRoot { get; }
            public string BranchRef { get; }
            public string? Repository { get; }

            public BuildRequest(string projectRoot, string branchRef, string? repository)
            {
                ProjectRoot = projectRoot;
                BranchRef = branchRef;
                Repository = repository;
            }
        }

        private sealed class WorkflowRun
        {
            public long Id { get; }
            public string Url { get; }

            public WorkflowRun(long id, string url)
            {
                Id = id;
                Url = url;
            }
        }

        private sealed class WorkflowStatus
        {
            public string Status { get; }
            public string Conclusion { get; }
            public string JobsSummary { get; }
            public float JobsProgress { get; }

            public WorkflowStatus(string status, string conclusion, string jobsSummary, float jobsProgress)
            {
                Status = status;
                Conclusion = conclusion;
                JobsSummary = jobsSummary;
                JobsProgress = jobsProgress;
            }
        }

        private sealed class ProcessResult
        {
            public int ExitCode { get; }
            public string Stdout { get; }
            public string Stderr { get; }
            public bool Succeeded => ExitCode == 0;
            public string Error => string.IsNullOrWhiteSpace(Stderr) ? Stdout : Stderr;

            public ProcessResult(int exitCode, string stdout, string stderr)
            {
                ExitCode = exitCode;
                Stdout = stdout;
                Stderr = stderr;
            }
        }
    }

    internal static class YargAudioPackageBuilderExtensions
    {
        internal static bool MatchesDispatchedRun(
            this JObject run,
            string branchRef,
            DateTimeOffset earliest)
        {
            var eventType = (string?) run["event"] ?? string.Empty;
            var createdAtText = (string?) run["createdAt"] ?? string.Empty;
            if (eventType != "workflow_dispatch" ||
                !DateTimeOffset.TryParse(
                    createdAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var createdAt) ||
                createdAt < earliest)
            {
                return false;
            }

            var headBranch = (string?) run["headBranch"] ?? string.Empty;
            var headSha = (string?) run["headSha"] ?? string.Empty;
            return string.Equals(headBranch, branchRef, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headSha, branchRef, StringComparison.OrdinalIgnoreCase) ||
                branchRef.EndsWith(headBranch, StringComparison.OrdinalIgnoreCase);
        }

        internal static string? ParseRepositorySlug(this string remoteUrl)
        {
            var cleanedUrl = remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? remoteUrl.Substring(0, remoteUrl.Length - 4)
                : remoteUrl;

            var lastSlash = cleanedUrl.LastIndexOfAny(new[] { '/', ':' });
            if (lastSlash > 0)
            {
                var prevSlash = cleanedUrl.LastIndexOfAny(new[] { '/', ':' }, lastSlash - 1);
                if (prevSlash >= 0)
                {
                    return cleanedUrl.Substring(prevSlash + 1);
                }
            }

            return null;
        }
    }
}
