using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using CodeTower.Interfaces;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeTower.Services;

public class RestructuringEngine : IRestructuringEngine
{
    private readonly ILogger _logger;
    private readonly IBackupService _backupService;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;
    private Solution _solution;
    private MSBuildWorkspace _workspace;
    private readonly List<string> _filesToDelete = new();

    public RestructuringEngine(
        ILogger logger,
        IBackupService backupService,
        IDependencyAnalyzer dependencyAnalyzer)
    {
        _logger = logger;
        _backupService = backupService;
        _dependencyAnalyzer = dependencyAnalyzer;
    }

    public async Task InitializeAsync(string solutionPath)
    {
        try
        {
            _logger.LogInformation($"Initializing solution: {solutionPath}");
            await _backupService.CreateBackupAsync(solutionPath);
            
            _workspace = MSBuildWorkspace.Create();
            _solution = await _workspace.OpenSolutionAsync(solutionPath);
            
            _logger.LogInformation("Solution initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize solution", ex);
            throw;
        }
    }

    public async Task ApplyTransformationAsync(Transformation transformation)
    {
        try
        {
            _logger.LogInformation($"Applying transformation: {transformation.Type}");
            
            // Validate transformation
            await ValidateTransformationAsync(transformation);
            
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

                default:
                    throw new NotSupportedException($"Transformation type not supported: {transformation.Type}");
            }
            
            _logger.LogInformation("Transformation applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to apply transformation: {transformation.Type}", ex);
            throw;
        }
    }

    public async Task CommitChangesAsync()
    {
        try
        {
            _logger.LogInformation("Committing changes...");
            
            if (!_workspace.TryApplyChanges(_solution))
            {
                throw new RestructuringException("Failed to apply solution changes");
            }

            foreach (var file in _filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    _logger.LogInformation($"Deleted file: {file}");
                }
            }
            
            _logger.LogInformation("Changes committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to commit changes", ex);
            throw;
        }
    }

    private async Task ValidateTransformationAsync(Transformation transformation)
    {
        var graph = await _dependencyAnalyzer.BuildDependencyGraphAsync(_solution);
        var conflicts = _dependencyAnalyzer.FindDependencyConflicts(
            transformation.Source,
            transformation.Target,
            graph);

        if (conflicts.Any())
        {
            var errorDetails = conflicts.Select(c => c.ToString());
            throw new RestructuringException(
                "Dependency conflicts detected",
                errorDetails);
        }
    }

    private async Task MoveNamespaceWithCodeGenerationAsync(string sourceNs, string targetNs)
    {
        _logger.LogInformation($"Moving namespace from {sourceNs} to {targetNs}");
        
        // Temporary namespace to avoid conflicts
        string tempNs = $"{sourceNs}.__TEMP_{Guid.NewGuid().ToString("N")[..6]}";
        await RenameNamespaceInternalAsync(sourceNs, tempNs);
        await RenameNamespaceInternalAsync(tempNs, targetNs);

        // Generate new namespace structure
        await GenerateNamespaceStructureAsync(targetNs);
        
        _logger.LogInformation($"Namespace moved successfully");
    }

    private async Task RenameNamespaceWithCodeGenerationAsync(string oldNs, string newNs)
    {
        _logger.LogInformation($"Renaming namespace from {oldNs} to {newNs}");
        
        await RenameNamespaceInternalAsync(oldNs, newNs);
        await GenerateNamespaceStructureAsync(newNs);
        
        _logger.LogInformation($"Namespace renamed successfully");
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

            await GeneratePhysicalStructureAsync(project, newNs);
        }
    }

    private async Task ExtractClassToNewFileAsync(string className, string targetNamespace)
    {
        _logger.LogInformation($"Extracting class {className} to namespace {targetNamespace}");
        
        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            var classSymbol = compilation.GetSymbolsWithName(className)
                .FirstOrDefault() as INamedTypeSymbol;

            if (classSymbol == null) continue;

            var sourceDocument = project.GetDocument(classSymbol.Locations.First().SourceTree);
            var syntaxRoot = await sourceDocument.GetSyntaxRootAsync();
            var classNode = syntaxRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.Text == className);

            // Generate new file
            var newFilePath = GenerateFilePath(project, targetNamespace, $"{className}.cs");
            var newContent = GenerateClassFile(classNode, targetNamespace);

            var newDocument = project.AddDocument(
                $"{className}.cs",
                newContent,
                PathToFolders(NamespaceToPath(targetNamespace)),
                newFilePath);

            // Update source file
            var newRoot = syntaxRoot.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia);
            var updatedDocument = sourceDocument.WithSyntaxRoot(newRoot);

            _solution = newDocument.Project.Solution
                .WithDocumentSyntaxRoot(sourceDocument.Id, newRoot);
        }
        
        _logger.LogInformation($"Class extracted successfully");
    }

    private async Task GenerateArchitectureLayerAsync(string layerName, string[] subfolders)
    {
        _logger.LogInformation($"Generating architecture layer: {layerName}");
        
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

        // Generate base structure
        await GenerateNamespaceStructureAsync(layerName);

        // Generate subfolders
        foreach (var folder in subfolders)
        {
            var fullNs = $"{layerName}.{folder}";
            await GenerateNamespaceStructureAsync(fullNs);
            await GenerateInitialClassAsync(
                project.Id,
                fullNs,
                Path.Combine(
                    NamespaceToPath(fullNs),
                    $"{folder}Service.cs"));
        }
        
        _logger.LogInformation($"Architecture layer generated successfully");
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

    private string NamespaceToPath(string ns) => 
        ns.Replace('.', Path.DirectorySeparatorChar);

    private IEnumerable<string> PathToFolders(string path) => 
        path.Split(Path.DirectorySeparatorChar);
} 

// Класс-движок реструктуризации
//public class RestructuringEngine : IRestructuringEngine
//{
//    private Solution _solution;
//    private MSBuildWorkspace _workspace;
//    private readonly List<string> _filesToDelete = new();

//    public async Task InitializeAsync(string solutionPath)
//    {
//        _workspace = MSBuildWorkspace.Create();
//        _solution = await _workspace.OpenSolutionAsync(solutionPath);
//    }

//    public async Task ApplyTransformationAsync(Transformation transformation)
//    {
//        switch (transformation.Type)
//        {
//            case TransformationType.MoveNamespace:
//                await MoveNamespaceWithCodeGenerationAsync(
//                    transformation.Source,
//                    transformation.Target);
//                break;

//            case TransformationType.RenameNamespace:
//                await RenameNamespaceWithCodeGenerationAsync(
//                    transformation.Source,
//                    transformation.Target);
//                break;

//            case TransformationType.ExtractClass:
//                await ExtractClassToNewFileAsync(
//                    transformation.Source,
//                    transformation.Target);
//                break;

//            case TransformationType.GenerateLayer:
//                await GenerateArchitectureLayerAsync(
//                    transformation.Target,
//                    transformation.Options["subfolders"]?.Split(',') ?? Array.Empty<string>());
//                break;
//        }
//    }

//    private async Task MoveNamespaceWithCodeGenerationAsync(string sourceNs, string targetNs)
//    {
//        // Ханойский подход: промежуточный шаг
//        string tempNs = $"{sourceNs}.__TEMP_{Guid.NewGuid().ToString("N")[..6]}";
//        await RenameNamespaceInternalAsync(sourceNs, tempNs);
//        await RenameNamespaceInternalAsync(tempNs, targetNs);

//        // Генерация нового слоя
//        await GenerateNamespaceStructureAsync(targetNs);
//    }

//    private async Task RenameNamespaceWithCodeGenerationAsync(string oldNs, string newNs)
//    {
//        await RenameNamespaceInternalAsync(oldNs, newNs);
//        await GenerateNamespaceStructureAsync(newNs);
//    }

//    private async Task RenameNamespaceInternalAsync(string oldNs, string newNs)
//    {
//        foreach (var project in _solution.Projects)
//        {
//            var compilation = await project.GetCompilationAsync();
//            var namespaces = NamespaceLocator.FindNamespaces(compilation, oldNs);

//            foreach (var nsSymbol in namespaces)
//            {
//                _solution = await Renamer.RenameSymbolAsync(
//                    _solution,
//                    nsSymbol,
//                    new SymbolRenameOptions(),
//                    newNs);
//            }

//            // Генерация физической структуры
//            await GeneratePhysicalStructureAsync(project, newNs);
//        }
//    }

//    private async Task GenerateNamespaceStructureAsync(string namespaceName)
//    {
//        var path = NamespaceToPath(namespaceName);
//        var projects = _solution.Projects.ToList();

//        foreach (var project in projects)
//        {
//            var projectPath = Path.GetDirectoryName(project.FilePath);
//            var fullPath = Path.Combine(projectPath, path);

//            if (!Directory.Exists(fullPath))
//            {
//                Directory.CreateDirectory(fullPath);

//                // Генерация начального класса
//                await GenerateInitialClassAsync(
//                    project.Id,
//                    namespaceName,
//                    Path.Combine(fullPath, "__NamespaceInitializer.cs"));
//            }
//        }
//    }

//    private async Task GeneratePhysicalStructureAsync(Project project, string namespaceName)
//    {
//        var oldPath = NamespaceToPath(namespaceName);
//        var newPath = NamespaceToPath(namespaceName);

//        foreach (var document in project.Documents.ToList())
//        {
//            if (document.Folders.Any(f => f.StartsWith(oldPath)))
//            {
//                var newFolders = document.Folders
//                    .Select(f => f.Replace(oldPath, newPath))
//                    .ToArray();

//                var newFilePath = document.FilePath.Replace(oldPath, newPath);

//                // Создание нового файла сгенерированного кода
//                var syntaxRoot = await document.GetSyntaxRootAsync();
//                var newDocument = project.AddDocument(
//                    document.Name,
//                    syntaxRoot,
//                    newFolders,
//                    newFilePath);

//                _filesToDelete.Add(document.FilePath);
//                project = newDocument.Project;
//            }
//        }

//        _solution = project.Solution;
//    }

//    private async Task GenerateInitialClassAsync(
//        ProjectId projectId,
//        string namespaceName,
//        string filePath)
//    {
//        var classCode = $@"namespace {namespaceName}
//{{
//    public class {Path.GetFileNameWithoutExtension(filePath)}
//    {{
//        public void Initialize() 
//        {{
//            // Auto-generated initializer
//        }}
//    }}
//}}";

//        var project = _solution.GetProject(projectId);
//        var folders = Path.GetDirectoryName(filePath)
//            .Split(Path.DirectorySeparatorChar)
//            .SkipWhile(p => p == Path.GetDirectoryName(project.FilePath))
//            .ToArray();

//        var document = project.AddDocument(
//            Path.GetFileName(filePath),
//            SourceText.From(classCode),
//            folders,
//            filePath);

//        _solution = document.Project.Solution;
//    }

//    private async Task ExtractClassToNewFileAsync(string className, string targetNamespace)
//    {
//        foreach (var project in _solution.Projects)
//        {
//            var classSymbol = (await project.GetCompilationAsync())
//                .GetSymbolsWithName(className).FirstOrDefault() as INamedTypeSymbol;

//            if (classSymbol == null) continue;

//            var sourceDocument = project.GetDocument(classSymbol.Locations.First().SourceTree);
//            var syntaxRoot = await sourceDocument.GetSyntaxRootAsync();
//            var classNode = syntaxRoot.DescendantNodes()
//                .OfType<ClassDeclarationSyntax>()
//                .First(c => c.Identifier.Text == className);

//            // Генерация нового файла
//            var newFilePath = GenerateFilePath(project, targetNamespace, $"{className}.cs");
//            var newContent = GenerateClassFile(classNode, targetNamespace);

//            var newDocument = project.AddDocument(
//                $"{className}.cs",
//                newContent,
//                PathToFolders(NamespaceToPath(targetNamespace)),
//                newFilePath);

//            // Обновление исходного файла
//            var newRoot = syntaxRoot.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia);
//            var updatedDocument = sourceDocument.WithSyntaxRoot(newRoot);

//            _solution = newDocument.Project.Solution
//                .WithDocumentSyntaxRoot(sourceDocument.Id, newRoot);
//        }
//    }

//    private async Task GenerateArchitectureLayerAsync(
//        string layerName,
//        string[] subfolders)
//    {
//        var projectInfo = ProjectInfo.Create(
//            ProjectId.CreateNewId(),
//            VersionStamp.Create(),
//            layerName,
//            layerName,
//            LanguageNames.CSharp,
//            filePath: Path.Combine(
//                Path.GetDirectoryName(_solution.FilePath),
//                layerName,
//                $"{layerName}.csproj"));

//        _solution = _solution.AddProject(projectInfo);
//        var project = _solution.GetProject(projectInfo.Id);

//        // Генерация базовой структуры
//        await GenerateNamespaceStructureAsync(layerName);

//        // Генерация подпапок
//        foreach (var folder in subfolders)
//        {
//            var fullNs = $"{layerName}.{folder}";
//            await GenerateNamespaceStructureAsync(fullNs);
//            await GenerateInitialClassAsync(project.Id, fullNs,
//                Path.Combine(
//                    NamespaceToPath(fullNs),
//                    $"{folder}Service.cs"));
//        }
//    }

//    private async Task MoveNamespaceAsync(string sourceNs, string targetNs)
//    {
//        var projects = _solution.Projects.ToList();

//        // 1. Генерация временного пространства имён
//        string tempNs = $"{sourceNs}.__Temp_{Guid.NewGuid().ToString("N")[..8]}";
//        await RenameNamespaceInternalAsync(sourceNs, tempNs);

//        // 2. Генерация целевого пространства имён
//        await RenameNamespaceInternalAsync(tempNs, targetNs);
//    }

//    private async Task RenameNamespaceAsync(string oldNs, string newNs)
//    {
//        await RenameNamespaceInternalAsync(oldNs, newNs);
//    }

//    private async Task UpdateProjectStructureAsync(Project project, string oldNs, string newNs)
//    {
//        var oldPath = NamespaceToPath(oldNs);
//        var newPath = NamespaceToPath(newNs);

//        foreach (var document in project.Documents.ToList())
//        {
//            if (document.Folders.Any(f => f.StartsWith(oldPath)))
//            {
//                var newFolders = document.Folders
//                    .Select(f => f.Replace(oldPath, newPath))
//                    .ToArray();

//                var newFilePath = document.FilePath.Replace(oldPath, newPath);

//                // Генерация нового файла
//                var syntaxRoot = await document.GetSyntaxRootAsync();
//                var newDocument = project.AddDocument(
//                    document.Name,
//                    syntaxRoot,
//                    newFolders,
//                    newFilePath);

//                // Помечаем старый файл на удаление
//                _filesToDelete.Add(document.FilePath);

//                project = newDocument.Project;
//            }
//        }

//        _solution = project.Solution;
//    }

//    public void CommitChangesAsync()
//    {
//        _workspace.TryApplyChanges(_solution);

//        // Удаление старых файлов
//        foreach (var file in _filesToDelete)
//        {
//            if (File.Exists(file)) File.Delete(file);
//        }
//    }

//    private string GenerateClassFile(ClassDeclarationSyntax classNode, string targetNamespace)
//    {
//        return $@"namespace {targetNamespace}
//{{
//    {classNode}
//}}";
//    }

//    private string GenerateFilePath(Project project, string ns, string fileName)
//    {
//        var projectDir = Path.GetDirectoryName(project.FilePath);
//        return Path.Combine(projectDir, NamespaceToPath(ns), fileName);
//    }

//    private string NamespaceToPath(string ns) => ns.Replace('.', Path.DirectorySeparatorChar);
//    private IEnumerable<string> PathToFolders(string path) => path.Split(Path.DirectorySeparatorChar);
//}
