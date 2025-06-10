namespace CodeTower.Interfaces;

public interface IBackupService
{
    Task CreateBackupAsync(string solutionPath);
    Task RestoreBackupAsync(string backupPath);
} 