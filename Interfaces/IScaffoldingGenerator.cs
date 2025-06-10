namespace CodeTower.Interfaces;

public interface IScaffoldingGenerator
{
    Task InitializeAsync(string solutionPath);
    Task GenerateArchitectureAsync(string template);
    Task CommitChangesAsync();
}
