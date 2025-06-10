namespace CodeTower.Models;

public class RestructuringException : Exception
{
    public IEnumerable<string> ErrorDetails { get; }

    public RestructuringException(string message, IEnumerable<string> details = null)
        : base(message)
    {
        ErrorDetails = details ?? Enumerable.Empty<string>();
    }
}