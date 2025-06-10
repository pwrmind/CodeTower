namespace CodeTower.Interfaces;

public interface IRestructuringEngine
{
    Task InitializeAsync(string solutionPath);
    Task ApplyTransformationAsync(Transformation transformation);
    Task CommitChangesAsync();
}
