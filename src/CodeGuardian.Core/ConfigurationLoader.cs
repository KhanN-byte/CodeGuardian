using System.Text.Json;

namespace CodeGuardian.Core;

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<CodeGuardianConfiguration> LoadAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new CodeGuardianConfiguration();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CodeGuardianConfiguration>(stream, JsonOptions, cancellationToken)
            ?? new CodeGuardianConfiguration();
    }
}
