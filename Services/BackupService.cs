using CodeTower.Interfaces;

namespace CodeTower.Services;

public class BackupService : IBackupService
{
    private readonly ILogger _logger;
    private const string BACKUP_FOLDER = ".codetower_backup";

    public BackupService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task CreateBackupAsync(string solutionPath)
    {
        try
        {
            var solutionDir = Path.GetDirectoryName(solutionPath);
            var backupDir = Path.Combine(solutionDir, BACKUP_FOLDER, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            _logger.LogInformation($"Creating backup in {backupDir}");
            
            Directory.CreateDirectory(backupDir);
            
            foreach (var file in Directory.GetFiles(solutionDir, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains(BACKUP_FOLDER)) continue;
                
                var relativePath = Path.GetRelativePath(solutionDir, file);
                var backupPath = Path.Combine(backupDir, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(file, backupPath);
            }
            
            _logger.LogInformation("Backup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create backup", ex);
            throw;
        }
    }

    public async Task RestoreBackupAsync(string backupPath)
    {
        try
        {
            var solutionDir = Path.GetDirectoryName(backupPath);
            _logger.LogInformation($"Restoring from backup {backupPath}");
            
            foreach (var file in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupPath, file);
                var targetPath = Path.Combine(solutionDir, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(file, targetPath, true);
            }
            
            _logger.LogInformation("Restore completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to restore backup", ex);
            throw;
        }
    }
} 