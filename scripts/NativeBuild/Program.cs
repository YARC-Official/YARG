using System.Diagnostics;
using System.Globalization;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            RepositoryLayout repository = RepositoryLayout.Find();
            string command = args[0].ToLowerInvariant();

            return command switch
            {
                "build" => await BuildCommand.RunAsync(
                    repository, ParseBuildOptions(args[1..])),
                "package" => await PackageCommand.RunAsync(
                    repository, ParsePackageOptions(args[1..])),
                _ => throw new ToolException($"Unknown command '{args[0]}'."),
            };
        }
        catch (ToolException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static BuildOptions ParseBuildOptions(string[] args)
    {
        string configuration = "Release";
        string? outputDirectory = null;
        bool noCopy = false;
        bool verifyCommittedPlugin = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--configuration":
                    configuration = ReadValue(args, ref i, "--configuration");
                    if (configuration is not ("Debug" or "Release" or "RelWithDebInfo"))
                    {
                        throw new ToolException(
                            "--configuration must be Debug, Release, or RelWithDebInfo.");
                    }

                    break;
                case "--output":
                    outputDirectory = ReadValue(args, ref i, "--output");
                    break;
                case "--no-copy":
                    noCopy = true;
                    break;
                case "--verify-committed-plugin":
                    verifyCommittedPlugin = true;
                    break;
                case "--help":
                    PrintBuildUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ToolException($"Unknown build option '{args[i]}'.");
            }
        }

        if (noCopy && outputDirectory is not null)
        {
            throw new ToolException("--no-copy and --output cannot be used together.");
        }

        if (verifyCommittedPlugin && noCopy)
        {
            throw new ToolException(
                "--verify-committed-plugin cannot be combined with --no-copy.");
        }

        return new BuildOptions(
            configuration, outputDirectory, noCopy, verifyCommittedPlugin);
    }

    private static PackageOptions ParsePackageOptions(string[] args)
    {
        string? remoteRef = null;
        string? repository = null;
        int timeoutMinutes = 45;
        int pollSeconds = 10;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ref":
                    remoteRef = ReadValue(args, ref i, "--ref");
                    break;
                case "--repo":
                    repository = ReadValue(args, ref i, "--repo");
                    break;
                case "--timeout-minutes":
                    string timeoutValue = ReadValue(args, ref i, "--timeout-minutes");
                    if (!int.TryParse(timeoutValue, NumberStyles.None,
                        CultureInfo.InvariantCulture, out timeoutMinutes) ||
                        timeoutMinutes < 1)
                    {
                        throw new ToolException("--timeout-minutes must be a positive integer.");
                    }

                    break;
                case "--poll-seconds":
                    string pollValue = ReadValue(args, ref i, "--poll-seconds");
                    if (!int.TryParse(pollValue, NumberStyles.None,
                        CultureInfo.InvariantCulture, out pollSeconds) ||
                        pollSeconds < 1)
                    {
                        throw new ToolException("--poll-seconds must be a positive integer.");
                    }

                    break;
                case "--help":
                    PrintPackageUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ToolException($"Unknown package option '{args[i]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(remoteRef))
        {
            throw new ToolException(
                "package requires --ref <remote-branch-or-commit>.");
        }

        return new PackageOptions(
            remoteRef, repository, timeoutMinutes, pollSeconds);
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ToolException($"{option} requires a value.");
        }

        return args[index];
    }

    private static bool IsHelp(string argument) =>
        argument is "--help" or "-h" or "help";

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project scripts/NativeBuild -- build [options]
              dotnet run --project scripts/NativeBuild -- package --ref <remote-ref>

            Commands:
              build       Build/test native plugin for current host and update Unity.
              package     Run GitHub all-platform build and update local Unity plugins.

            No command performs local Git operations.
            """);
        PrintBuildUsage();
        PrintPackageUsage();
    }

    private static void PrintBuildUsage()
    {
        Console.WriteLine(
            """
            build options:
              --configuration <Debug|Release|RelWithDebInfo>  Default: Release.
              --output <directory>                             Write artifact files there.
              --no-copy                                         Do not copy plugin files.
              --verify-committed-plugin                         Test/validate existing Unity plugin.
            """);
    }

    private static void PrintPackageUsage()
    {
        Console.WriteLine(
            """
            package options:
              --ref <remote-branch-or-commit>  Required source ref for GitHub Actions.
              --repo <owner/repo>               Optional GitHub repository override.
              --timeout-minutes <n>             Default: 45.
              --poll-seconds <n>                Default: 10.
            """);
    }
}

internal sealed class ToolException : Exception
{
    public ToolException(string message) : base(message)
    {
    }
}

internal sealed record BuildOptions(
    string Configuration,
    string? OutputDirectory,
    bool NoCopy,
    bool VerifyCommittedPlugin);

internal sealed record PackageOptions(
    string RemoteRef,
    string? Repository,
    int TimeoutMinutes,
    int PollSeconds);

internal sealed class RepositoryLayout
{
    private RepositoryLayout(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string NativeDirectory =>
        Path.Combine(Root, "Native", "YargAudio");

    public static RepositoryLayout Find()
    {
        string[] starts =
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        foreach (string start in starts)
        {
            DirectoryInfo? directory = new(Path.GetFullPath(start));
            while (directory is not null)
            {
                string cmakeFile = Path.Combine(
                    directory.FullName, "Native", "YargAudio", "CMakeLists.txt");
                if (File.Exists(cmakeFile))
                {
                    return new RepositoryLayout(directory.FullName);
                }

                directory = directory.Parent;
            }
        }

        throw new ToolException(
            "Could not find repository root containing Native/YargAudio/CMakeLists.txt.");
    }
}

internal static class ProcessRunner
{
    public static async Task RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory)
    {
        CommandResult result = await ExecuteAsync(
            fileName, arguments, workingDirectory, captureOutput: false);
        if (result.ExitCode != 0)
        {
            throw new ToolException(
                $"{fileName} failed with exit code {result.ExitCode}.");
        }
    }

    public static async Task<CommandResult> CaptureAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory)
    {
        return await ExecuteAsync(
            fileName, arguments, workingDirectory, captureOutput: true);
    }

    private static async Task<CommandResult> ExecuteAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        bool captureOutput)
    {
        string[] argumentArray = arguments.ToArray();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            CreateNoWindow = true,
        };

        foreach (string argument in argumentArray)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Console.WriteLine($"> {FormatCommand(fileName, argumentArray)}");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new ToolException($"Could not start '{fileName}'.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new ToolException(
                $"Could not start '{fileName}'. Is it installed and on PATH? " +
                exception.Message);
        }

        if (!captureOutput)
        {
            await process.WaitForExitAsync();
            return new CommandResult(process.ExitCode, string.Empty, string.Empty);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        return new CommandResult(
            process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
    {
        static string Quote(string value)
        {
            if (value.Length == 0 || value.Any(char.IsWhiteSpace) || value.Contains('"'))
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }

        return string.Join(' ', new[] { fileName }.Concat(arguments).Select(Quote));
    }
}

internal sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
