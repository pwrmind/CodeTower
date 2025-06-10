using Microsoft.CodeAnalysis;

namespace CodeTower.Interfaces;

public interface IDependencyAnalyzer
{
    Task<DependencyGraph> BuildDependencyGraphAsync(Solution solution);
    IEnumerable<DependencyConflict> FindDependencyConflicts(string sourceNs, string targetNs, DependencyGraph graph);
}
