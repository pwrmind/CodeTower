using CodeTower.Interfaces;

namespace CodeTower.Services;

public class ConsoleLogger : ILogger
{
    public void LogInformation(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[INFO] {message}");
        Console.ForegroundColor = originalColor;
    }

    public void LogWarning(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ForegroundColor = originalColor;
    }

    public void LogError(string message, Exception exception = null)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception.Message}");
            Console.WriteLine($"StackTrace: {exception.StackTrace}");
        }
        Console.ForegroundColor = originalColor;
    }
} 