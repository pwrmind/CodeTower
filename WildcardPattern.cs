using System.Text.RegularExpressions;

namespace CodeTower;

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
