using CodeTower.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeTower.Services;

// Генератор каркаса архитектуры с улучшенной кодогенерацией
public class ScaffoldingGenerator : IScaffoldingGenerator
{
    private Solution _solution;
    private MSBuildWorkspace _workspace;
    private Project _mainProject;

    public async Task InitializeAsync(string solutionPath)
    {
        _workspace = MSBuildWorkspace.Create();
        _solution = await _workspace.OpenSolutionAsync(solutionPath);
        _mainProject = _solution.Projects.First();
    }

    public async Task GenerateArchitectureAsync(string template)
    {
        // Генерация слоёв с кодогенерацией
        await GenerateLayerAsync("Domain", new[] { "Entities", "ValueObjects", "Interfaces" });
        await GenerateLayerAsync("Application", new[] { "UseCases", "Interfaces", "DTOs", "Services" });
        await GenerateLayerAsync("Infrastructure", new[] { "Data", "Services", "Repositories", "External" });
        await GenerateLayerAsync("Presentation", Array.Empty<string>());

        // Добавление ссылок
        await AddProjectReferencesAsync();

        // Генерация базовых классов
        await GenerateBaseEntitiesAsync();
    }

    private async Task GenerateLayerAsync(string layerName, string[] subfolders)
    {
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            layerName,
            layerName,
            LanguageNames.CSharp,
            filePath: Path.Combine(
                Path.GetDirectoryName(_mainProject.FilePath),
                layerName,
                $"{layerName}.csproj"));

        _solution = _solution.AddProject(projectInfo);
        var project = _solution.GetProject(projectId);

        // Генерация базового namespace
        await AddClassToProjectAsync(project, layerName, $"{layerName}Initializer.cs");

        // Генерация подпапок
        foreach (var folder in subfolders)
        {
            var ns = $"{layerName}.{folder}";
            await AddClassToProjectAsync(
                project,
                ns,
                Path.Combine(folder, $"{folder}Service.cs"));
        }
    }

    private async Task AddClassToProjectAsync(
        Project project,
        string ns,
        string relativePath)
    {
        var className = Path.GetFileNameWithoutExtension(relativePath);
        var content = $@"namespace {ns}
{{
    public class {className}
    {{
        // Auto-generated class
        public void Execute() {{ }}
    }}
}}";

        var fullPath = Path.Combine(
            Path.GetDirectoryName(project.FilePath),
            relativePath);

        var document = project.AddDocument(
            Path.GetFileName(relativePath),
            content,
            folders: Path.GetDirectoryName(relativePath)?
                .Split(Path.DirectorySeparatorChar) ?? Array.Empty<string>(),
            filePath: fullPath);

        project = document.Project;
        _solution = project.Solution;
    }

    private async Task GenerateBaseEntitiesAsync()
    {
        var domainProject = _solution.Projects.First(p => p.Name == "Domain");
        var entityCode = @"namespace Domain.Entities
{
    public abstract class EntityBase
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    }
}";

        await AddClassToProjectAsync(
            domainProject,
            "Domain.Entities",
            Path.Combine("Entities", "EntityBase.cs"));
    }

    private async Task AddProjectReferencesAsync()
    {
        var projects = _solution.Projects.ToDictionary(p => p.Name, p => p.Id);

        // Application -> Domain
        _solution = _solution.AddProjectReference(
            projects["Application"],
            new ProjectReference(projects["Domain"]));

        // Infrastructure -> Application, Domain
        _solution = _solution.AddProjectReference(
            projects["Infrastructure"],
            new ProjectReference(projects["Application"]));
        _solution = _solution.AddProjectReference(
            projects["Infrastructure"],
            new ProjectReference(projects["Domain"]));

        // Presentation -> Application
        _solution = _solution.AddProjectReference(
            projects["Presentation"],
            new ProjectReference(projects["Application"]));
    }

    public async Task CommitChangesAsync() =>
        _workspace.TryApplyChanges(_solution);

}
