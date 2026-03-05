using System.Diagnostics;

var profile = args.Length > 0 ? args[0] : "all";

var validProfiles = new Dictionary<string, string>
{
    ["unit"] = "backend-unit-tests",
    ["integration"] = "backend-integration-tests",
    ["frontend"] = "frontend-tests",
    ["backend"] = "backend-unit-tests",
    ["all"] = "backend-unit-tests"
};

if (!validProfiles.TryGetValue(profile, out var exitFrom))
{
    Console.WriteLine("Usage: TestRunner [unit|integration|backend|frontend|all]");
    return 1;
}

var dockerDir = FindDockerDir();
var composeFile = Path.Combine(dockerDir, "docker-compose.test.yml");

Console.WriteLine($"Running tests: {profile}");
Console.WriteLine("---");

var upExitCode = await RunProcessAsync(
    "docker",
    $"compose -f \"{composeFile}\" --progress plain --profile {profile} up --build --abort-on-container-exit --exit-code-from {exitFrom}",
    dockerDir
);

await RunProcessAsync(
    "docker",
    $"compose -f \"{composeFile}\" --progress plain --profile {profile} down",
    dockerDir,
    suppressOutput: true
);

Console.WriteLine();
if (upExitCode == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("--- All tests passed ---");
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"--- Tests failed (exit code: {upExitCode}) ---");
}
Console.ResetColor();

return upExitCode;

static string FindDockerDir()
{
    // Walk up from current directory looking for docker-compose.test.yml
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "docker-compose.test.yml")))
            return dir;

        var dockerSubDir = Path.Combine(dir, "docker");
        if (File.Exists(Path.Combine(dockerSubDir, "docker-compose.test.yml")))
            return dockerSubDir;

        dir = Directory.GetParent(dir)?.FullName;
    }

    // Fallback to current directory
    return Directory.GetCurrentDirectory();
}

static async Task<int> RunProcessAsync(string fileName, string arguments, string workingDir, bool suppressOutput = false)
{
    using var process = new Process();
    process.StartInfo = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDir,
        UseShellExecute = false,
        RedirectStandardOutput = suppressOutput,
        RedirectStandardError = suppressOutput
    };

    process.Start();

    if (suppressOutput)
    {
        await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
}
