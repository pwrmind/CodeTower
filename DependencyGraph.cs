namespace CodeTower;

// Вспомогательные модели
public class DependencyGraph
{
    private readonly Dictionary<string, List<string>> _dependencies = new();

    public void AddDependency(string from, string to)
    {
        if (!_dependencies.ContainsKey(from))
            _dependencies[from] = new List<string>();

        if (!_dependencies[from].Contains(to))
            _dependencies[from].Add(to);
    }

    public IEnumerable<string> GetDependencies(string ns) =>
        _dependencies.TryGetValue(ns, out var deps) ? deps : Enumerable.Empty<string>();

    public bool HasCycle(string start, string current, HashSet<string> visited = null)
    {
        visited ??= new HashSet<string>();
        if (visited.Contains(current)) return true;

        visited.Add(current);
        foreach (var dep in GetDependencies(current))
        {
            if (HasCycle(start, dep, visited))
                return true;
        }
        return false;
    }

    public void Merge(DependencyGraph other)
    {
        foreach (var kvp in other._dependencies)
        {
            foreach (var dep in kvp.Value)
            {
                AddDependency(kvp.Key, dep);
            }
        }
    }
}
