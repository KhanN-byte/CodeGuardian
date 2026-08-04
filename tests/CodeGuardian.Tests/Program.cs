using CodeGuardian.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var tests = new (string Name, Func<Task> Run)[]
{
    ("glob patterns match project names", TestPatternMatching),
    ("architecture rules detect forbidden references", TestArchitectureRule),
    ("risky-code rules detect async blocking and swallowed errors", TestRiskyCode),
    ("API comparison classifies removals as breaking", TestApiComparison)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static Task TestPatternMatching()
{
    Assert(PatternMatcher.Matches("Shop.Domain", "*.Domain"), "Expected suffix wildcard to match.");
    Assert(!PatternMatcher.Matches("Shop.Api", "*.Domain"), "Unexpected match.");
    return Task.CompletedTask;
}

static Task TestArchitectureRule()
{
    using var workspace = new AdhocWorkspace();
    var infrastructureId = ProjectId.CreateNewId();
    var domainId = ProjectId.CreateNewId();
    var solution = workspace.CurrentSolution
        .AddProject(ProjectInfo.Create(infrastructureId, VersionStamp.Create(), "Shop.Infrastructure", "Shop.Infrastructure", LanguageNames.CSharp))
        .AddProject(ProjectInfo.Create(domainId, VersionStamp.Create(), "Shop.Domain", "Shop.Domain", LanguageNames.CSharp))
        .AddProjectReference(domainId, new ProjectReference(infrastructureId));

    var config = new CodeGuardianConfiguration
    {
        ArchitectureRules =
        [
            new ArchitectureRule { Source = "*.Domain", CannotReference = "*.Infrastructure" }
        ]
    };

    var findings = new ArchitectureAnalyzer().Analyze(solution, config);
    Assert(findings.Count == 1 && findings[0].RuleId == "CG001", "Expected one CG001 finding.");
    return Task.CompletedTask;
}

static async Task TestRiskyCode()
{
    const string source = """
        using System;
        using System.Threading.Tasks;

        public class Risky
        {
            public async void FireAndForget() { await Task.Delay(1); }
            public int Block() => Task.FromResult(42).Result;
            public void Wait() => Task.Delay(1).Wait();
            public void Swallow() { try { throw new Exception(); } catch { } }
        }
        """;

    using var workspace = new AdhocWorkspace();
    var projectId = ProjectId.CreateNewId();
    var solution = workspace.CurrentSolution.AddProject(
        ProjectInfo.Create(projectId, VersionStamp.Create(), "RiskyProject", "RiskyProject", LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp14),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: PlatformReferences()));
    solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "Risky.cs", source);

    var findings = await new RiskyCodeAnalyzer().AnalyzeAsync(solution.GetProject(projectId)!);
    Assert(findings.Count(f => f.RuleId == "CG101") == 1, "Expected async-void finding.");
    Assert(findings.Count(f => f.RuleId == "CG102") == 2, "Expected two blocking-task findings.");
    Assert(findings.Count(f => f.RuleId == "CG103") == 1, "Expected empty-catch finding.");
}

static Task TestApiComparison()
{
    var before = new ApiSnapshot(1, DateTimeOffset.UtcNow, new SortedDictionary<string, string[]>
    {
        ["Library"] = ["type class Library.Widget", "member Library.Widget::void Library.Widget.Run()"]
    });
    var after = new ApiSnapshot(1, DateTimeOffset.UtcNow, new SortedDictionary<string, string[]>
    {
        ["Library"] = ["type class Library.Widget"]
    });

    var changes = new ApiSurfaceAnalyzer().Compare(before, after);
    Assert(changes.Count == 1 && changes[0].Severity == FindingSeverity.Error, "Expected a breaking removal.");
    return Task.CompletedTask;
}

static IEnumerable<MetadataReference> PlatformReferences()
{
    var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    return paths.Select(path => MetadataReference.CreateFromFile(path));
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
