namespace CodeTower.Models;

public class Transformation
{
    public TransformationType Type { get; set; }
    public string Source { get; set; }
    public string Target { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
}
