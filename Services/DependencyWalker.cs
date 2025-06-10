using CodeTower.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTower.Services;

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
