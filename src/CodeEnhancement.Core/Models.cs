using System.Text.Json.Serialization;

namespace CodeEnhancement.Core;

[JsonConverter(typeof(JsonStringEnumConverter<FindingSeverity>))]
public enum FindingSeverity
{
    Info,
    Warning,
    Error
}

public sealed record Finding(
    string RuleId,
    FindingSeverity Severity,
    string Message,
    string Project,
    string? FilePath = null,
    int? Line = null,
    int? Column = null);

public sealed record AnalysisReport(
    string Target,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<Finding> Findings)
{
    public int ErrorCount => Findings.Count(f => f.Severity == FindingSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == FindingSeverity.Warning);
}

public sealed class CodeEnhancementConfiguration
{
    public List<ArchitectureRule> ArchitectureRules { get; init; } = [];
}

public sealed class ArchitectureRule
{
    public required string Source { get; init; }
    public required string CannotReference { get; init; }
    public string? Message { get; init; }
}

public sealed record ApiSnapshot(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    SortedDictionary<string, string[]> Projects);

public sealed record ApiChange(
    string Kind,
    string Project,
    string Symbol,
    FindingSeverity Severity);
