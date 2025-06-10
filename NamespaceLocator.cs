using Microsoft.CodeAnalysis;

namespace CodeTower;

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
