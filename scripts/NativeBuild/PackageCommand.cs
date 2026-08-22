using System.Globalization;
using System.Text.Json;

internal static class PackageCommand
{
    // Native audio workflow is registered on existing repositories. It now
    // publishes all three platform artifacts before its committed-plugin gate.
    private const string WorkflowFile = "native-audio.yml";

    public static async Task<int> RunAsync(
        RepositoryLayout repository,
        PackageOptions options)
    {
        DateTimeOffset dispatchStarted = DateTimeOffset.UtcNow;
        CommandResult dispatch = await GhAsync(
            repository,
            options,
            [
                "workflow",
                "run",
                WorkflowFile,
                "--ref",
                options.RemoteRef,
            ]);

        PrintCommandOutput(dispatch);
        WorkflowRun run = await FindDispatchedRunAsync(
            repository, options, dispatchStarted);
        Console.WriteLine($"GitHub Actions run: {run.Url}");

        await WaitForRunAsync(repository, run, options);

        string downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            $"yarg-audio-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(downloadDirectory);

        try
        {
            CommandResult download = await GhAsync(
                repository,
                options,
                [
                    "run",
                    "download",
                    run.DatabaseId.ToString(CultureInfo.InvariantCulture),
                    "--pattern",
                    "yarg-audio-*",
                    "--dir",
                    downloadDirectory,
                ]);
            PrintCommandOutput(download);

            IReadOnlyList<DownloadedPlugin> plugins =
                FindAndValidateArtifacts(downloadDirectory);
            foreach (DownloadedPlugin downloaded in plugins)
            {
                CopyToUnity(repository, downloaded);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(downloadDirectory, recursive: true);
            }
            catch (IOException)
            {
                Console.Error.WriteLine(
                    $"warning: could not remove temporary directory {downloadDirectory}");
            }
        }

        Console.WriteLine(
            "All platform plugins updated. Files remain unstaged and uncommitted.");
        return 0;
    }

    private static async Task<WorkflowRun> FindDispatchedRunAsync(
        RepositoryLayout repository,
        PackageOptions options,
        DateTimeOffset dispatchStarted)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(
            options.TimeoutMinutes);
        DateTimeOffset earliestAccepted = dispatchStarted.AddSeconds(-30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            CommandResult list = await GhAsync(
                repository,
                options,
                [
                    "run",
                    "list",
                    "--workflow",
                    WorkflowFile,
                    "--limit",
                    "50",
                    "--json",
                    "databaseId,headBranch,headSha,status,conclusion,createdAt,url,event,displayTitle",
                ]);

            List<WorkflowRun> candidates = ParseRuns(
                list.StandardOutput)
                .Where(candidate =>
                    candidate.CreatedAt >= earliestAccepted &&
                    candidate.Event == "workflow_dispatch")
                .ToList();

            WorkflowRun? matching = candidates
                .Where(candidate => MatchesRef(candidate, options.RemoteRef))
                .OrderByDescending(candidate => candidate.CreatedAt)
                .FirstOrDefault();
            if (matching is not null)
            {
                return matching;
            }

            // Some GitHub responses do not populate headBranch for a SHA ref.
            // Dispatch timestamp still isolates this invocation from older runs.
            WorkflowRun? newest = candidates
                .OrderByDescending(candidate => candidate.CreatedAt)
                .FirstOrDefault();
            if (newest is not null)
            {
                return newest;
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
        }

        throw new ToolException(
            "Timed out waiting for GitHub Actions to create package run.");
    }

    private static async Task WaitForRunAsync(
        RepositoryLayout repository,
        WorkflowRun run,
        PackageOptions options)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(
            options.TimeoutMinutes);
        string? previousStatus = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            CommandResult view = await GhAsync(
                repository,
                options,
                [
                    "run",
                    "view",
                    run.DatabaseId.ToString(CultureInfo.InvariantCulture),
                    "--json",
                    "status,conclusion,url",
                ]);
            WorkflowStatus status = ParseStatus(view.StandardOutput);

            if (status.Status != previousStatus)
            {
                Console.WriteLine(
                    $"Package run {run.DatabaseId}: {status.Status}");
                previousStatus = status.Status;
            }

            if (status.Status == "completed")
            {
                if (status.Conclusion != "success")
                {
                    Console.Error.WriteLine(
                        $"warning: package run completed with conclusion " +
                        $"'{status.Conclusion}'; checking uploaded artifacts. {status.Url}");
                }

                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds));
        }

        throw new ToolException(
            $"Timed out waiting for GitHub Actions package run {run.DatabaseId}.");
    }

    private static IReadOnlyList<DownloadedPlugin> FindAndValidateArtifacts(
        string downloadDirectory)
    {
        var downloaded = new List<DownloadedPlugin>();
        foreach (NativePlugin plugin in NativePlugins.All)
        {
            string binary = FindArtifactFile(
                downloadDirectory, plugin.ArtifactName, plugin.BinaryName);
            string metadata = FindArtifactFile(
                downloadDirectory, plugin.ArtifactName, plugin.BinaryName + ".meta");

            RequireArtifactFile(binary, plugin.BinaryName);
            RequireArtifactFile(metadata, plugin.BinaryName + ".meta");
            downloaded.Add(new DownloadedPlugin(plugin, binary, metadata));
        }

        return downloaded;
    }

    private static string FindArtifactFile(
        string downloadDirectory,
        string artifactName,
        string fileName)
    {
        string artifactDirectory = Path.Combine(downloadDirectory, artifactName);
        IEnumerable<string> searchRoots = Directory.Exists(artifactDirectory)
            ? [artifactDirectory]
            : [downloadDirectory];

        string[] matches = searchRoots
            .SelectMany(root => Directory.Exists(root)
                ? Directory.EnumerateFiles(
                    root, fileName, SearchOption.AllDirectories)
                : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ToolException(
                $"GitHub artifact '{artifactName}' is missing '{fileName}'.");
        }

        if (matches.Length > 1)
        {
            throw new ToolException(
                $"GitHub artifact '{artifactName}' contains multiple '{fileName}' files.");
        }

        return matches[0];
    }

    private static void RequireArtifactFile(string path, string fileName)
    {
        if (new FileInfo(path).Length == 0)
        {
            throw new ToolException($"Downloaded artifact file is empty: {fileName}");
        }
    }

    private static void CopyToUnity(
        RepositoryLayout repository,
        DownloadedPlugin downloaded)
    {
        string destinationDirectory = Path.Combine(
            repository.Root, downloaded.Plugin.PluginDirectory);
        Directory.CreateDirectory(destinationDirectory);

        string destinationBinary = Path.Combine(
            destinationDirectory, downloaded.Plugin.BinaryName);
        File.Copy(downloaded.BinaryPath, destinationBinary, overwrite: true);
        File.Copy(
            downloaded.MetadataPath,
            destinationBinary + ".meta",
            overwrite: true);

        Console.WriteLine(
            $"Updated {downloaded.Plugin.BinaryName} in {destinationDirectory}");
    }

    private static bool MatchesRef(WorkflowRun run, string remoteRef)
    {
        string normalizedRef = remoteRef;
        if (normalizedRef.StartsWith("refs/heads/", StringComparison.Ordinal))
        {
            normalizedRef = normalizedRef["refs/heads/".Length..];
        }

        return string.Equals(run.HeadSha, remoteRef,
                   StringComparison.OrdinalIgnoreCase) ||
            string.Equals(run.HeadBranch, remoteRef, StringComparison.Ordinal) ||
            string.Equals(run.HeadBranch, normalizedRef, StringComparison.Ordinal);
    }

    private static List<WorkflowRun> ParseRuns(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement
                .EnumerateArray()
                .Select(ParseRun)
                .ToList();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            throw new ToolException(
                $"Could not parse GitHub Actions run list: {exception.Message}");
        }
    }

    private static WorkflowRun ParseRun(JsonElement element)
    {
        return new WorkflowRun(
            element.GetProperty("databaseId").GetInt64(),
            element.GetProperty("headBranch").GetString(),
            element.GetProperty("headSha").GetString(),
            element.GetProperty("createdAt").GetDateTimeOffset(),
            element.GetProperty("status").GetString() ?? string.Empty,
            element.GetProperty("conclusion").GetString(),
            element.GetProperty("url").GetString() ?? string.Empty,
            element.GetProperty("event").GetString() ?? string.Empty);
    }

    private static WorkflowStatus ParseStatus(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            return new WorkflowStatus(
                root.GetProperty("status").GetString() ?? string.Empty,
                root.GetProperty("conclusion").GetString(),
                root.GetProperty("url").GetString() ?? string.Empty);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            throw new ToolException(
                $"Could not parse GitHub Actions run status: {exception.Message}");
        }
    }

    private static async Task<CommandResult> GhAsync(
        RepositoryLayout repository,
        PackageOptions options,
        IEnumerable<string> arguments)
    {
        List<string> ghArguments = [];
        if (options.Repository is not null)
        {
            ghArguments.Add("-R");
            ghArguments.Add(options.Repository);
        }

        ghArguments.AddRange(arguments);
        CommandResult result = await ProcessRunner.CaptureAsync(
            "gh", ghArguments, repository.Root);
        if (result.ExitCode != 0)
        {
            string details = string.Join(
                Environment.NewLine,
                new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }
                    .Where(text => text.Length > 0));
            throw new ToolException(
                $"gh command failed with exit code {result.ExitCode}." +
                (details.Length == 0 ? string.Empty : $"{Environment.NewLine}{details}"));
        }

        return result;
    }

    private static void PrintCommandOutput(CommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.WriteLine(result.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Console.Error.WriteLine(result.StandardError.Trim());
        }
    }

    private sealed record DownloadedPlugin(
        NativePlugin Plugin,
        string BinaryPath,
        string MetadataPath);

    private sealed record WorkflowRun(
        long DatabaseId,
        string? HeadBranch,
        string? HeadSha,
        DateTimeOffset CreatedAt,
        string Status,
        string? Conclusion,
        string Url,
        string Event);

    private sealed record WorkflowStatus(
        string Status,
        string? Conclusion,
        string Url);
}
