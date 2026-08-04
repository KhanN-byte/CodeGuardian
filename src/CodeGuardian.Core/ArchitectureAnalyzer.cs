using Microsoft.CodeAnalysis;

namespace CodeGuardian.Core;

public sealed class ArchitectureAnalyzer
{
    public IReadOnlyList<Finding> Analyze(Solution solution, CodeGuardianConfiguration configuration)
    {
        var findings = new List<Finding>();
        var projectById = solution.Projects.ToDictionary(project => project.Id);

        foreach (var project in solution.Projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                if (!projectById.TryGetValue(reference.ProjectId, out var referencedProject))
                {
                    continue;
                }

                foreach (var rule in configuration.ArchitectureRules)
                {
                    if (!PatternMatcher.Matches(project.Name, rule.Source) ||
                        !PatternMatcher.Matches(referencedProject.Name, rule.CannotReference))
                    {
                        continue;
                    }

                    findings.Add(new Finding(
                        "CG001",
                        FindingSeverity.Error,
                        rule.Message ?? $"Project '{project.Name}' must not reference '{referencedProject.Name}'.",
                        project.Name,
                        project.FilePath));
                }
            }
        }

        return findings;
    }
}
