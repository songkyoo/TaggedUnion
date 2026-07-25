#!/usr/bin/env -S dotnet --

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

const int maxArity = 8;

var scriptDirectory = Path.GetDirectoryName(GetSourceFilePath()) ?? throw new InvalidOperationException("Unable to determine the script directory.");
var repositoryRoot = Directory.GetParent(scriptDirectory)?.FullName ?? throw new InvalidOperationException("Unable to determine the repository root.");
var taggedUnionDirectory = Path.Combine(repositoryRoot, "TaggedUnion");
var transforms = new[]
{
    (Template: "ITaggedUnionCases.tt", Output: "ITaggedUnionCases.g.cs"),
    (Template: "ITaggedUnion.tt", Output: "ITaggedUnion.g.cs"),
    (Template: "TaggedUnion.tt", Output: "TaggedUnion.g.cs"),
};

var exitCode = await RunDotnetAsync(repositoryRoot, "tool", "restore");

if (exitCode != 0)
{
    return exitCode;
}

foreach (var transform in transforms)
{
    var templatePath = Path.Combine(taggedUnionDirectory, transform.Template);
    var outputPath = Path.Combine(taggedUnionDirectory, transform.Output);

    Console.WriteLine($"Generating {transform.Output}...");

    exitCode = await RunDotnetAsync(
        repositoryRoot,
        "tool",
        "run",
        "t4",
        $"--parameter=MaxArity={maxArity.ToString(CultureInfo.InvariantCulture)}",
        templatePath,
        "-o",
        outputPath
    );

    if (exitCode != 0)
    {
        return exitCode;
    }

    await NormalizeGeneratedSourceAsync(outputPath);
}

Console.WriteLine($"Tagged union sources through arity {maxArity} generated successfully.");

return 0;

static string GetSourceFilePath([CallerFilePath] string path = "")
{
    return path;
}

static async Task<int> RunDotnetAsync(string workingDirectory, params string[] arguments)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        UseShellExecute = false,
        WorkingDirectory = workingDirectory,
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the dotnet process.");

    await process.WaitForExitAsync();

    return process.ExitCode;
}

static async Task NormalizeGeneratedSourceAsync(string path)
{
    var source = await File.ReadAllTextAsync(path);
    var normalizedSource = source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .TrimEnd('\n') + "\n";

    await File.WriteAllTextAsync(
        path,
        contents: normalizedSource,
        encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    );
}
