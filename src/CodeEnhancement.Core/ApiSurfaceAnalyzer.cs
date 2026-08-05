using Microsoft.CodeAnalysis;

namespace CodeEnhancement.Core;

public sealed class ApiSurfaceAnalyzer
{
    public async Task<ApiSnapshot> CreateSnapshotAsync(
        Solution solution,
        CancellationToken cancellationToken = default)
    {
        var projects = new SortedDictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var project in solution.Projects.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            projects[project.Name] = EnumerateTypes(compilation.Assembly.GlobalNamespace)
                .Where(IsExternallyVisible)
                .SelectMany(DescribeTypeAndMembers)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return new ApiSnapshot(1, DateTimeOffset.UtcNow, projects);
    }

    public IReadOnlyList<ApiChange> Compare(ApiSnapshot baseline, ApiSnapshot current)
    {
        var changes = new List<ApiChange>();
        var projectNames = baseline.Projects.Keys.Union(current.Projects.Keys, StringComparer.Ordinal);

        foreach (var projectName in projectNames.Order(StringComparer.Ordinal))
        {
            var before = baseline.Projects.GetValueOrDefault(projectName) ?? [];
            var after = current.Projects.GetValueOrDefault(projectName) ?? [];

            changes.AddRange(before.Except(after, StringComparer.Ordinal)
                .Select(symbol => new ApiChange("removed", projectName, symbol, FindingSeverity.Error)));
            changes.AddRange(after.Except(before, StringComparer.Ordinal)
                .Select(symbol => new ApiChange("added", projectName, symbol, FindingSeverity.Info)));
        }

        return changes;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in EnumerateNestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in EnumerateTypes(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol parent)
    {
        foreach (var nested in parent.GetTypeMembers())
        {
            yield return nested;
            foreach (var descendant in EnumerateNestedTypes(nested))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        if (type.ContainingType is null)
        {
            return type.DeclaredAccessibility == Accessibility.Public;
        }

        return IsExternallyVisible(type.ContainingType) && IsPublicOrProtected(type.DeclaredAccessibility);
    }

    private static IEnumerable<string> DescribeTypeAndMembers(INamedTypeSymbol type)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        yield return $"type {type.TypeKind.ToString().ToLowerInvariant()} {typeName}";

        foreach (var member in type.GetMembers()
                     .Where(m => !m.IsImplicitlyDeclared && IsPublicOrProtected(m.DeclaredAccessibility)))
        {
            yield return $"member {typeName}::{member.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}";
        }
    }

    private static bool IsPublicOrProtected(Accessibility accessibility) => accessibility is
        Accessibility.Public or
        Accessibility.Protected or
        Accessibility.ProtectedOrInternal;
}
