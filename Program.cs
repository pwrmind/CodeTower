using CodeTower.Interfaces;
using CodeTower.Models;
using CodeTower.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace CodeTower;

public class Program
{
    private static async Task Main(string[] args)
    {
        var services = ConfigureServices();

        var rootCommand = new RootCommand("CodeTower Architecture Restructuring Tool");
        ConfigureCommands(rootCommand, services);

        await rootCommand.InvokeAsync(args);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IDependencyAnalyzer, DependencyAnalyzer>();
        services.AddSingleton<IRestructuringEngine, RestructuringEngine>();
        services.AddSingleton<IScaffoldingGenerator, ScaffoldingGenerator>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureCommands(RootCommand rootCommand, IServiceProvider services)
    {
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
            await HandleRestructureCommand(services, solution, config);
        }, solutionOption, configOption);

        generateCommand.SetHandler(async (solution, template) =>
        {
            await HandleGenerateCommand(services, solution, template);
        }, solutionOption, new Option<string>("--template"));
    }

    private static async Task HandleRestructureCommand(
        IServiceProvider services,
        FileInfo solutionFile,
        FileInfo configFile)
    {
        var logger = services.GetRequiredService<ILogger>();
        var engine = services.GetRequiredService<IRestructuringEngine>();

        try
        {
            var config = await RestructuringConfig.LoadAsync(configFile);
            await engine.InitializeAsync(solutionFile.FullName);

            foreach (var transformation in config.Transformations)
            {
                await engine.ApplyTransformationAsync(transformation);
            }

            await engine.CommitChangesAsync();
            logger.LogInformation("Restructuring completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError("Restructuring failed", ex);
            Environment.Exit(1);
        }
    }

    private static async Task HandleGenerateCommand(
        IServiceProvider services,
        FileInfo solutionFile,
        string templateName)
    {
        var logger = services.GetRequiredService<ILogger>();
        var generator = services.GetRequiredService<IScaffoldingGenerator>();

        try
        {
            await generator.InitializeAsync(solutionFile.FullName);
            await generator.GenerateArchitectureAsync(templateName);
            await generator.CommitChangesAsync();
            logger.LogInformation("Scaffolding generated successfully");
        }
        catch (Exception ex)
        {
            logger.LogError("Scaffolding generation failed", ex);
            Environment.Exit(1);
        }
    }
}
