using Microsoft.CodeAnalysis;

namespace CodeEnhancement.Core;

public sealed class SolutionAnalysisService
{
    private readonly ArchitectureAnalyzer architectureAnalyzer = new();
    private readonly RiskyCodeAnalyzer riskyCodeAnalyzer = new();

    public async Task<AnalysisReport> AnalyzeAsync(
        Solution solution,
        string target,
        CodeEnhancementConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var findings = architectureAnalyzer.Analyze(solution, configuration).ToList();

        foreach (var project in solution.Projects)
        {
            findings.AddRange(await riskyCodeAnalyzer.AnalyzeAsync(project, cancellationToken));
        }

        return new AnalysisReport(
            target,
            DateTimeOffset.UtcNow,
            findings.OrderBy(f => f.FilePath, StringComparer.Ordinal)
                .ThenBy(f => f.Line)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .ToArray());
    }
}
