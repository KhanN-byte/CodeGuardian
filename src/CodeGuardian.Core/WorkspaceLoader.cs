using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeGuardian.Core;

public static class WorkspaceLoader
{
    public static async Task<(MSBuildWorkspace Workspace, Solution Solution)> LoadAsync(
        string target,
        Action<string>? onWorkspaceFailure = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(target);
        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(args => onWorkspaceFailure?.Invoke(args.Diagnostic.Message));

        try
        {
            Solution solution;
            if (Path.GetExtension(fullPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
                solution = project.Solution;
            }
            else
            {
                solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
            }

            return (workspace, solution);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }
}
