using System.Text.Json;

namespace CodeEnhancement.Core;

public static class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<CodeEnhancementConfiguration> LoadAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new CodeEnhancementConfiguration();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CodeEnhancementConfiguration>(stream, JsonOptions, cancellationToken)
            ?? new CodeEnhancementConfiguration();
    }
}
