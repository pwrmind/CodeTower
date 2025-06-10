using System.Text.Json;

namespace CodeTower;

public class RestructuringConfig
{
    public List<Transformation> Transformations { get; set; } = new();

    public static async Task<RestructuringConfig> LoadAsync(FileInfo configFile) =>
        JsonSerializer.Deserialize<RestructuringConfig>(
            await File.ReadAllTextAsync(configFile.FullName));
}
