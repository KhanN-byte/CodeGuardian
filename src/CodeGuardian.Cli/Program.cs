using System.Text.Json;
using System.Text.Json.Serialization;
using CodeGuardian.Core;
using Microsoft.Build.Locator;

return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            return args[0] switch
            {
                "analyze" => await AnalyzeAsync(args[1..]),
                "baseline" => await BaselineAsync(args[1..]),
                "api-diff" => await ApiDiffAsync(args[1..]),
                _ => UnknownCommand(args[0])
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"codeguardian: {exception.Message}");
            return 2;
        }
    }

    private static async Task<int> AnalyzeAsync(string[] args)
    {
        var target = RequiredPositional(args, "analyze <solution-or-project>");
        var configPath = Option(args, "--config") ?? FindDefaultConfiguration(target);
        var format = Option(args, "--format") ?? "text";
        var configuration = await ConfigurationLoader.LoadAsync(configPath);

        var (workspace, solution) = await WorkspaceLoader.LoadAsync(
            target,
            warning => Console.Error.WriteLine($"workspace: {warning}"));
        using (workspace)
        {
            var report = await new SolutionAnalysisService().AnalyzeAsync(solution, target, configuration);
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            }
            else
            {
                PrintReport(report);
            }

            return report.ErrorCount > 0 ? 1 : 0;
        }
    }

    private static async Task<int> BaselineAsync(string[] args)
    {
        var target = RequiredPositional(args, "baseline <solution-or-project> --output <file>");
        var output = Option(args, "--output")
            ?? throw new ArgumentException("Missing required option --output <file>.");

        var (workspace, solution) = await WorkspaceLoader.LoadAsync(target);
        using (workspace)
        {
            var snapshot = await new ApiSurfaceAnalyzer().CreateSnapshotAsync(solution);
            var outputPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            Console.WriteLine($"Wrote API baseline for {snapshot.Projects.Count} project(s) to {outputPath}");
            return 0;
        }
    }

    private static async Task<int> ApiDiffAsync(string[] args)
    {
        var target = RequiredPositional(args, "api-diff <solution-or-project> --baseline <file>");
        var baselinePath = Option(args, "--baseline")
            ?? throw new ArgumentException("Missing required option --baseline <file>.");
        var format = Option(args, "--format") ?? "text";

        var baseline = JsonSerializer.Deserialize<ApiSnapshot>(
            await File.ReadAllTextAsync(baselinePath),
            JsonOptions) ?? throw new InvalidDataException("The API baseline is empty or invalid.");

        var (workspace, solution) = await WorkspaceLoader.LoadAsync(target);
        using (workspace)
        {
            var analyzer = new ApiSurfaceAnalyzer();
            var current = await analyzer.CreateSnapshotAsync(solution);
            var changes = analyzer.Compare(baseline, current);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(JsonSerializer.Serialize(changes, JsonOptions));
            }
            else if (changes.Count == 0)
            {
                Console.WriteLine("No public API changes detected.");
            }
            else
            {
                foreach (var change in changes)
                {
                    Console.WriteLine($"{change.Severity,-7} {change.Kind,-7} {change.Project}: {change.Symbol}");
                }
            }

            return changes.Any(c => c.Severity == FindingSeverity.Error) ? 1 : 0;
        }
    }

    private static void PrintReport(AnalysisReport report)
    {
        foreach (var finding in report.Findings)
        {
            var location = finding.FilePath is null
                ? finding.Project
                : $"{finding.FilePath}:{finding.Line ?? 1}:{finding.Column ?? 1}";
            Console.WriteLine($"{location}: {finding.Severity.ToString().ToLowerInvariant()} {finding.RuleId}: {finding.Message}");
        }

        Console.WriteLine($"Analyzed {report.Target}: {report.ErrorCount} error(s), {report.WarningCount} warning(s).");
    }

    private static string RequiredPositional(string[] args, string usage)
    {
        if (args.Length == 0 || args[0].StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Usage: codeguardian {usage}");
        }

        return args[0];
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {name}.");
        }

        return args[index + 1];
    }

    private static string? FindDefaultConfiguration(string target)
    {
        var targetPath = Path.GetFullPath(target);
        var directory = File.Exists(targetPath) ? Path.GetDirectoryName(targetPath)! : targetPath;
        var candidate = Path.Combine(directory, "codeguardian.json");
        return File.Exists(candidate) ? candidate : null;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            CodeGuardian — Roslyn-powered architecture and API analysis

            Usage:
              codeguardian analyze <solution-or-project> [--config <file>] [--format text|json]
              codeguardian baseline <solution-or-project> --output <file>
              codeguardian api-diff <solution-or-project> --baseline <file> [--format text|json]

            Rules:
              CG001  Forbidden project reference
              CG101  async void method
              CG102  Blocking Task.Result or Task.Wait()
              CG103  Empty catch block
            """);
    }
}
