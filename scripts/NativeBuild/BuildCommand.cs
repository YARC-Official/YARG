using System.Buffers;

internal enum NativePlatform
{
    Windows,
    Linux,
    MacOS,
}

internal sealed record NativePlugin(
    NativePlatform Platform,
    string IntegrationPlatform,
    string ConfigurePreset,
    string BuildPreset,
    string TestPreset,
    string BinaryName,
    string PluginDirectory,
    string BuiltDirectory,
    string ArtifactName)
{
    public string PluginBinaryPath(RepositoryLayout repository) =>
        Path.Combine(repository.Root, PluginDirectory, BinaryName);

    public string PluginMetadataPath(RepositoryLayout repository) =>
        PluginBinaryPath(repository) + ".meta";

    public string MetadataSourcePath(RepositoryLayout repository) =>
        PluginMetadataPath(repository);

    public string BuiltBinaryPath(
        RepositoryLayout repository, string configuration) =>
        Path.Combine(
            repository.NativeDirectory,
            BuiltDirectory,
            Platform == NativePlatform.Windows ? configuration : string.Empty,
            BinaryName);
}

internal static class NativePlugins
{
    public static IReadOnlyList<NativePlugin> All { get; } =
    [
        new(
            NativePlatform.Windows,
            "Windows",
            "windows-x64",
            "windows-x64-release",
            "windows-x64-release",
            "yarg_audio.dll",
            "Assets/Plugins/YargAudio/Windows/x86_64",
            "build/windows-x64",
            "yarg-audio-windows-x64"),
        new(
            NativePlatform.Linux,
            "Linux",
            "linux-x64",
            "linux-x64-release",
            "linux-x64-release",
            "libyarg_audio.so",
            "Assets/Plugins/YargAudio/Linux/x86_64",
            "build/linux-x64",
            "yarg-audio-linux-x64"),
        new(
            NativePlatform.MacOS,
            "MacOS",
            "macos-universal",
            "macos-universal-release",
            "macos-universal-release",
            "libyarg_audio.dylib",
            "Assets/Plugins/YargAudio/Mac",
            "build/macos-universal",
            "yarg-audio-macos-universal"),
    ];

    public static NativePlugin ForCurrentHost()
    {
        NativePlatform platform =
            OperatingSystem.IsWindows()
                ? NativePlatform.Windows
                : OperatingSystem.IsLinux()
                    ? NativePlatform.Linux
                    : OperatingSystem.IsMacOS()
                        ? NativePlatform.MacOS
                        : throw new ToolException(
                            "Unsupported host. NativeBuild supports Windows, Linux, and macOS.");

        return All.Single(plugin => plugin.Platform == platform);
    }
}

internal static class BuildCommand
{
    public static async Task<int> RunAsync(
        RepositoryLayout repository,
        BuildOptions options)
    {
        NativePlugin plugin = NativePlugins.ForCurrentHost();
        if (plugin.Platform != NativePlatform.Windows &&
            options.Configuration != "Release")
        {
            throw new ToolException(
                $"{plugin.Platform} build currently supports Release configuration only.");
        }

        string native = repository.NativeDirectory;
        Console.WriteLine($"Building {plugin.Platform} native plugin.");

        await ProcessRunner.RunAsync(
            "cmake",
            ["--preset", plugin.ConfigurePreset],
            native);

        List<string> buildArguments =
        [
            "--build",
            "--preset",
            plugin.BuildPreset,
            "--parallel",
        ];
        if (plugin.Platform == NativePlatform.Windows)
        {
            buildArguments.Add("--config");
            buildArguments.Add(options.Configuration);
        }

        await ProcessRunner.RunAsync("cmake", buildArguments, native);

        List<string> testArguments =
        [
            "--preset",
            plugin.TestPreset,
        ];
        if (plugin.Platform == NativePlatform.Windows)
        {
            testArguments.Add("-C");
            testArguments.Add(options.Configuration);
        }

        await ProcessRunner.RunAsync("ctest", testArguments, native);

        string builtBinary = plugin.BuiltBinaryPath(
            repository, options.Configuration);
        RequireFile(builtBinary, "Native build output");

        if (plugin.Platform == NativePlatform.MacOS)
        {
            await ProcessRunner.RunAsync(
                "lipo",
                [builtBinary, "-verify_arch", "x86_64", "arm64"],
                repository.Root);
        }

        await RunIntegrationAsync(
            repository, plugin, options.Configuration, builtBinary);

        if (options.OutputDirectory is not null)
        {
            string destination = Path.GetFullPath(options.OutputDirectory, repository.Root);
            CopyPlugin(repository, plugin, builtBinary, destination);
        }

        if (options.VerifyCommittedPlugin)
        {
            await VerifyCommittedPluginAsync(
                repository, plugin, options.Configuration, builtBinary);
        }
        else if (options.OutputDirectory is null && !options.NoCopy)
        {
            string destination = Path.GetDirectoryName(plugin.PluginBinaryPath(repository))!;
            CopyPlugin(repository, plugin, builtBinary, destination);
        }

        Console.WriteLine("Native build completed.");
        return 0;
    }

    private static async Task VerifyCommittedPluginAsync(
        RepositoryLayout repository,
        NativePlugin plugin,
        string configuration,
        string builtBinary)
    {
        string committedBinary = plugin.PluginBinaryPath(repository);
        string committedMetadata = plugin.PluginMetadataPath(repository);
        string metadataSource = plugin.MetadataSourcePath(repository);

        RequireFile(committedBinary, "Committed Unity plugin");
        RequireFile(committedMetadata, "Committed Unity plugin metadata");
        RequireFile(metadataSource, "Unity plugin metadata source");

        if (plugin.Platform != NativePlatform.Windows &&
            !FilesEqual(builtBinary, committedBinary))
        {
            throw new ToolException(
                $"Committed {plugin.BinaryName} does not match native build output.");
        }

        if (!FilesEqual(metadataSource, committedMetadata))
        {
            throw new ToolException(
                $"Committed {plugin.BinaryName}.meta does not match metadata source.");
        }

        await RunIntegrationAsync(
            repository, plugin, configuration, committedBinary);
        Console.WriteLine(
            $"Committed {plugin.BinaryName} passed native integration.");
    }

    private static async Task RunIntegrationAsync(
        RepositoryLayout repository,
        NativePlugin plugin,
        string configuration,
        string libraryPath)
    {
        List<string> arguments =
        [
            "run",
            "--project",
            "tests/GainIntegration/GainIntegration.csproj",
            "--configuration",
            "Release",
            $"-p:YargAudioPlatform={plugin.IntegrationPlatform}",
            $"-p:YargAudioNativeConfiguration={configuration}",
            $"-p:YargAudioLibraryPath={libraryPath}",
        ];

        await ProcessRunner.RunAsync("dotnet", arguments, repository.NativeDirectory);
    }

    private static void CopyPlugin(
        RepositoryLayout repository,
        NativePlugin plugin,
        string builtBinary,
        string destinationDirectory)
    {
        string metadataSource = plugin.MetadataSourcePath(repository);
        RequireFile(metadataSource, "Unity plugin metadata source");

        Directory.CreateDirectory(destinationDirectory);
        string destinationBinary = Path.Combine(
            destinationDirectory, plugin.BinaryName);
        string destinationMetadata = destinationBinary + ".meta";

        File.Copy(builtBinary, destinationBinary, overwrite: true);
        if (!PathsEqual(metadataSource, destinationMetadata))
        {
            File.Copy(metadataSource, destinationMetadata, overwrite: true);
        }

        Console.WriteLine($"Copied {plugin.BinaryName} to {destinationDirectory}");
    }

    private static bool PathsEqual(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    private static void RequireFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new ToolException($"{description} is missing: {path}");
        }
    }

    private static bool FilesEqual(string first, string second)
    {
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        using FileStream firstStream = File.OpenRead(first);
        using FileStream secondStream = File.OpenRead(second);
        byte[] firstBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        byte[] secondBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (true)
            {
                int firstRead = firstStream.Read(firstBuffer, 0, firstBuffer.Length);
                int secondRead = secondStream.Read(
                    secondBuffer, 0, secondBuffer.Length);
                if (firstRead != secondRead)
                {
                    return false;
                }

                if (firstRead == 0)
                {
                    return true;
                }

                if (!firstBuffer.AsSpan(0, firstRead).SequenceEqual(
                    secondBuffer.AsSpan(0, secondRead)))
                {
                    return false;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(firstBuffer);
            ArrayPool<byte>.Shared.Return(secondBuffer);
        }
    }
}
