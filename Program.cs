using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Конфигурация CLI
        var rootCommand = new RootCommand("DotNet Architecture Restructuring Tool");

        var solutionOption = new Option<FileInfo>("--solution", "Path to solution file") { IsRequired = true };
        var configOption = new Option<FileInfo>("--config", "Path to restructuring config file");

        var restructureCommand = new Command("restructure", "Perform solution restructuring");
        restructureCommand.AddOption(solutionOption);
        restructureCommand.AddOption(configOption);

        var generateCommand = new Command("generate", "Generate scaffolding for architecture");
        generateCommand.AddOption(solutionOption);
        generateCommand.AddOption(new Option<string>("--template", "Architecture template name"));

        rootCommand.AddCommand(restructureCommand);
        rootCommand.AddCommand(generateCommand);

        restructureCommand.SetHandler(async (solution, config) =>
        {
            await RestructureSolutionAsync(solution, config);
        }, solutionOption, configOption);

        generateCommand.SetHandler(async (solution, template) =>
        {
            await GenerateScaffoldingAsync(solution, template);
        }, solutionOption, new Option<string>("--template"));

        await rootCommand.InvokeAsync(args);

        // Основные методы
        async Task RestructureSolutionAsync(FileInfo solutionFile, FileInfo configFile)
        {
            var config = await RestructuringConfig.LoadAsync(configFile);
            var engine = new RestructuringEngine();
            await engine.InitializeAsync(solutionFile.FullName);

            foreach (var transformation in config.Transformations)
            {
                await engine.ApplyTransformationAsync(transformation);
            }

            engine.CommitChangesAsync();
        }

        async Task GenerateScaffoldingAsync(FileInfo solutionFile, string templateName)
        {
            var generator = new ScaffoldingGenerator();
            await generator.InitializeAsync(solutionFile.FullName);

            switch (templateName.ToLower())
            {
                case "cleanarchitecture":
                    await generator.GenerateCleanArchitectureAsync();
                    break;
                case "onion":
                    //await generator.GenerateOnionArchitectureAsync();
                    break;
                case "verticalslice":
                    //await generator.GenerateVerticalSliceAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown template: {templateName}");
            }

            generator.CommitChangesAsync();
        }
    }
}

//Реализация поиска пространств имён
public static class NamespaceLocator
{
    public static IEnumerable<INamespaceSymbol> FindNamespaces(Compilation compilation, string namespaceName)
    {
        var globalNamespace = compilation.GlobalNamespace;
        var results = new List<INamespaceSymbol>();

        FindNamespacesRecursive(globalNamespace, namespaceName, results);
        return results;
    }

    private static void FindNamespacesRecursive(
        INamespaceSymbol currentNamespace,
        string targetName,
        List<INamespaceSymbol> results)
    {
        if (currentNamespace.ToDisplayString() == targetName)
        {
            results.Add(currentNamespace);
        }

        foreach (var subNamespace in currentNamespace.GetNamespaceMembers())
        {
            FindNamespacesRecursive(subNamespace, targetName, results);
        }
    }

    public static IEnumerable<INamespaceSymbol> FindNamespacesByPattern(
        Compilation compilation,
        string pattern)
    {
        var regex = new WildcardPattern(pattern);
        return compilation.GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(ns => FindMatchingNamespaces(ns, regex))
            .ToList();
    }

    public static IEnumerable<INamespaceSymbol> FindMatchingNamespaces(
        INamespaceSymbol ns,
        WildcardPattern pattern)
    {
        if (pattern.IsMatch(ns.ToDisplayString()))
        {
            yield return ns;
        }

        foreach (var subNs in ns.GetNamespaceMembers())
        {
            foreach (var match in FindMatchingNamespaces(subNs, pattern))
            {
                yield return match;
            }
        }
    }
}

public class WildcardPattern
{
    string _pattern;
    public WildcardPattern(string pattern)
    {
        _pattern = pattern;
    }
    public bool IsMatch(string input)
    {
        string regexPattern = "^" + Regex.Escape(_pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regexPattern);
    }
}

// Класс-движок реструктуризации
public class RestructuringEngine
{
    private Solution _solution;
    private MSBuildWorkspace _workspace;
    private readonly List<string> _filesToDelete = new();

    public async Task InitializeAsync(string solutionPath)
    {
        _workspace = MSBuildWorkspace.Create();
        _solution = await _workspace.OpenSolutionAsync(solutionPath);
    }

    public async Task ApplyTransformationAsync(Transformation transformation)
    {
        switch (transformation.Type)
        {
            case TransformationType.MoveNamespace:
                await MoveNamespaceWithCodeGenerationAsync(
                    transformation.Source,
                    transformation.Target);
                break;

            case TransformationType.RenameNamespace:
                await RenameNamespaceWithCodeGenerationAsync(
                    transformation.Source,
                    transformation.Target);
                break;

            case TransformationType.ExtractClass:
                await ExtractClassToNewFileAsync(
                    transformation.Source,
                    transformation.Target);
                break;

            case TransformationType.GenerateLayer:
                await GenerateArchitectureLayerAsync(
                    transformation.Target,
                    transformation.Options["subfolders"]?.Split(',') ?? Array.Empty<string>());
                break;
        }
    }

    private async Task MoveNamespaceWithCodeGenerationAsync(string sourceNs, string targetNs)
    {
        // Ханойский подход: промежуточный шаг
        string tempNs = $"{sourceNs}.__TEMP_{Guid.NewGuid().ToString("N")[..6]}";
        await RenameNamespaceInternalAsync(sourceNs, tempNs);
        await RenameNamespaceInternalAsync(tempNs, targetNs);

        // Генерация нового слоя
        await GenerateNamespaceStructureAsync(targetNs);
    }

    private async Task RenameNamespaceWithCodeGenerationAsync(string oldNs, string newNs)
    {
        await RenameNamespaceInternalAsync(oldNs, newNs);
        await GenerateNamespaceStructureAsync(newNs);
    }

    private async Task RenameNamespaceInternalAsync(string oldNs, string newNs)
    {
        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            var namespaces = NamespaceLocator.FindNamespaces(compilation, oldNs);

            foreach (var nsSymbol in namespaces)
            {
                _solution = await Renamer.RenameSymbolAsync(
                    _solution,
                    nsSymbol,
                    new SymbolRenameOptions(),
                    newNs);
            }

            // Генерация физической структуры
            await GeneratePhysicalStructureAsync(project, newNs);
        }
    }

    private async Task GenerateNamespaceStructureAsync(string namespaceName)
    {
        var path = NamespaceToPath(namespaceName);
        var projects = _solution.Projects.ToList();

        foreach (var project in projects)
        {
            var projectPath = Path.GetDirectoryName(project.FilePath);
            var fullPath = Path.Combine(projectPath, path);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);

                // Генерация начального класса
                await GenerateInitialClassAsync(
                    project.Id,
                    namespaceName,
                    Path.Combine(fullPath, "__NamespaceInitializer.cs"));
            }
        }
    }

    private async Task GeneratePhysicalStructureAsync(Project project, string namespaceName)
    {
        var oldPath = NamespaceToPath(namespaceName);
        var newPath = NamespaceToPath(namespaceName);

        foreach (var document in project.Documents.ToList())
        {
            if (document.Folders.Any(f => f.StartsWith(oldPath)))
            {
                var newFolders = document.Folders
                    .Select(f => f.Replace(oldPath, newPath))
                    .ToArray();

                var newFilePath = document.FilePath.Replace(oldPath, newPath);

                // Создание нового файла сгенерированного кода
                var syntaxRoot = await document.GetSyntaxRootAsync();
                var newDocument = project.AddDocument(
                    document.Name,
                    syntaxRoot,
                    newFolders,
                    newFilePath);

                _filesToDelete.Add(document.FilePath);
                project = newDocument.Project;
            }
        }

        _solution = project.Solution;
    }

    private async Task GenerateInitialClassAsync(
        ProjectId projectId,
        string namespaceName,
        string filePath)
    {
        var classCode = $@"namespace {namespaceName}
{{
    public class {Path.GetFileNameWithoutExtension(filePath)}
    {{
        public void Initialize() 
        {{
            // Auto-generated initializer
        }}
    }}
}}";

        var project = _solution.GetProject(projectId);
        var folders = Path.GetDirectoryName(filePath)
            .Split(Path.DirectorySeparatorChar)
            .SkipWhile(p => p == Path.GetDirectoryName(project.FilePath))
            .ToArray();

        var document = project.AddDocument(
            Path.GetFileName(filePath),
            SourceText.From(classCode),
            folders,
            filePath);

        _solution = document.Project.Solution;
    }

    private async Task ExtractClassToNewFileAsync(string className, string targetNamespace)
    {
        foreach (var project in _solution.Projects)
        {
            var classSymbol = (await project.GetCompilationAsync())
                .GetSymbolsWithName(className).FirstOrDefault() as INamedTypeSymbol;

            if (classSymbol == null) continue;

            var sourceDocument = project.GetDocument(classSymbol.Locations.First().SourceTree);
            var syntaxRoot = await sourceDocument.GetSyntaxRootAsync();
            var classNode = syntaxRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == className);

            // Генерация нового файла
            var newFilePath = GenerateFilePath(project, targetNamespace, $"{className}.cs");
            var newContent = GenerateClassFile(classNode, targetNamespace);

            var newDocument = project.AddDocument(
                $"{className}.cs",
                newContent,
                PathToFolders(NamespaceToPath(targetNamespace)),
                newFilePath);

            // Обновление исходного файла
            var newRoot = syntaxRoot.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia);
            var updatedDocument = sourceDocument.WithSyntaxRoot(newRoot);

            _solution = newDocument.Project.Solution
                .WithDocumentSyntaxRoot(sourceDocument.Id, newRoot);
        }
    }

    private async Task GenerateArchitectureLayerAsync(
        string layerName,
        string[] subfolders)
    {
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            layerName,
            layerName,
            LanguageNames.CSharp,
            filePath: Path.Combine(
                Path.GetDirectoryName(_solution.FilePath),
                layerName,
                $"{layerName}.csproj"));

        _solution = _solution.AddProject(projectInfo);
        var project = _solution.GetProject(projectInfo.Id);

        // Генерация базовой структуры
        await GenerateNamespaceStructureAsync(layerName);

        // Генерация подпапок
        foreach (var folder in subfolders)
        {
            var fullNs = $"{layerName}.{folder}";
            await GenerateNamespaceStructureAsync(fullNs);
            await GenerateInitialClassAsync(project.Id, fullNs,
                Path.Combine(
                    NamespaceToPath(fullNs),
                    $"{folder}Service.cs"));
        }
    }

    private async Task MoveNamespaceAsync(string sourceNs, string targetNs)
    {
        var projects = _solution.Projects.ToList();

        // 1. Генерация временного пространства имён
        string tempNs = $"{sourceNs}.__Temp_{Guid.NewGuid().ToString("N")[..8]}";
        await RenameNamespaceInternalAsync(sourceNs, tempNs);

        // 2. Генерация целевого пространства имён
        await RenameNamespaceInternalAsync(tempNs, targetNs);
    }

    private async Task RenameNamespaceAsync(string oldNs, string newNs)
    {
        await RenameNamespaceInternalAsync(oldNs, newNs);
    }

    private async Task UpdateProjectStructureAsync(Project project, string oldNs, string newNs)
    {
        var oldPath = NamespaceToPath(oldNs);
        var newPath = NamespaceToPath(newNs);

        foreach (var document in project.Documents.ToList())
        {
            if (document.Folders.Any(f => f.StartsWith(oldPath)))
            {
                var newFolders = document.Folders
                    .Select(f => f.Replace(oldPath, newPath))
                    .ToArray();

                var newFilePath = document.FilePath.Replace(oldPath, newPath);

                // Генерация нового файла
                var syntaxRoot = await document.GetSyntaxRootAsync();
                var newDocument = project.AddDocument(
                    document.Name,
                    syntaxRoot,
                    newFolders,
                    newFilePath);

                // Помечаем старый файл на удаление
                _filesToDelete.Add(document.FilePath);

                project = newDocument.Project;
            }
        }

        _solution = project.Solution;
    }

    public void CommitChangesAsync()
    {
        _workspace.TryApplyChanges(_solution);

        // Удаление старых файлов
        foreach (var file in _filesToDelete)
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private string GenerateClassFile(ClassDeclarationSyntax classNode, string targetNamespace)
    {
        return $@"namespace {targetNamespace}
{{
    {classNode}
}}";
    }

    private string GenerateFilePath(Project project, string ns, string fileName)
    {
        var projectDir = Path.GetDirectoryName(project.FilePath);
        return Path.Combine(projectDir, NamespaceToPath(ns), fileName);
    }

    private string NamespaceToPath(string ns) => ns.Replace('.', Path.DirectorySeparatorChar);
    private IEnumerable<string> PathToFolders(string path) => path.Split(Path.DirectorySeparatorChar);
}

// Генератор каркаса архитектуры с улучшенной кодогенерацией
public class ScaffoldingGenerator
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

    public async Task GenerateCleanArchitectureAsync()
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
// Модели конфигурации
// Модели конфигурации с новыми типами преобразований
public enum TransformationType 
{ 
    MoveNamespace, 
    RenameNamespace, 
    ExtractClass,
    GenerateLayer
}

public class Transformation
{
    public TransformationType Type { get; set; }
    public string Source { get; set; }
    public string Target { get; set; }
    public Dictionary<string, string> Options { get; set; } = new();
}

public class RestructuringConfig
{
    public List<Transformation> Transformations { get; set; } = new();

    public static async Task<RestructuringConfig> LoadAsync(FileInfo configFile) => 
        JsonSerializer.Deserialize<RestructuringConfig>(
            await File.ReadAllTextAsync(configFile.FullName));
}

// Анализатор зависимостей
public class DependencyAnalyzer
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

public class DependencyConflict
{
    private string sourceNs;
    private string dependency;
    private string v;

    public DependencyConflict(string sourceNs, string dependency, string v)
    {
        this.sourceNs = sourceNs;
        this.dependency = dependency;
        this.v = v;
    }
}

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


public class DependencyWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _model;
    public readonly DependencyGraph Dependencies = new();

    public DependencyWalker(SemanticModel model) => _model = model;

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbol = _model.GetSymbolInfo(node).Symbol;
        if (symbol?.ContainingNamespace != null)
        {
            var sourceNs = _model.GetEnclosingSymbol(node.SpanStart).Name;
            var targetNs = symbol.ContainingNamespace.ToDisplayString();

            if (!string.IsNullOrEmpty(sourceNs) && sourceNs != targetNs)
            {
                Dependencies.AddDependency(sourceNs, targetNs);
            }
        }
        base.VisitIdentifierName(node);
    }
}

public class RestructuringException : Exception
{
    public IEnumerable<string> ErrorDetails { get; }

    public RestructuringException(string message, IEnumerable<string> details = null)
        : base(message)
    {
        ErrorDetails = details ?? Enumerable.Empty<string>();
    }
}