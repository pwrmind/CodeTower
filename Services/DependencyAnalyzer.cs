using CodeTower.Interfaces;
using CodeTower.Models;
using Microsoft.CodeAnalysis;

namespace CodeTower.Services;

// Анализатор зависимостей
public class DependencyAnalyzer : IDependencyAnalyzer
{
    public async Task<DependencyGraph> BuildDependencyGraphAsync(Solution solution)
    {
        var graph = new DependencyGraph();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);
                var walker = new DependencyWalker(model);
                walker.Visit(await tree.GetRootAsync());
                graph.Merge(walker.Dependencies);
            }
        }

        return graph;
    }

    public IEnumerable<DependencyConflict> FindDependencyConflicts(
        string sourceNs,
        string targetNs,
        DependencyGraph graph)
    {
        var conflicts = new List<DependencyConflict>();
        var sourceDependencies = graph.GetDependencies(sourceNs);

        foreach (var dependency in sourceDependencies)
        {
            // Проверка нарушения слоёв архитектуры
            if (IsLayerViolation(sourceNs, targetNs, dependency))
            {
                conflicts.Add(new DependencyConflict(
                    sourceNs,
                    dependency,
                    $"Dependency violates architecture layers: {dependency}"));
            }

            // Проверка циклических зависимостей
            if (graph.HasCycle(sourceNs, dependency))
            {
                conflicts.Add(new DependencyConflict(
                    sourceNs,
                    dependency,
                    $"Cyclic dependency detected: {sourceNs} <-> {dependency}"));
            }
        }

        return conflicts;
    }

    private bool IsLayerViolation(string sourceNs, string targetNs, string dependency)
    {
        // Пример: Domain не должен зависеть от Application
        var targetLayer = GetArchitectureLayer(targetNs);
        var dependencyLayer = GetArchitectureLayer(dependency);

        return dependencyLayer > targetLayer; // Нарушение направления зависимостей
    }

    private int GetArchitectureLayer(string ns)
    {
        if (ns.Contains(".Domain.")) return 0;
        if (ns.Contains(".Application.")) return 1;
        if (ns.Contains(".Infrastructure.")) return 2;
        if (ns.Contains(".Presentation.")) return 3;
        return 4;
    }
}
