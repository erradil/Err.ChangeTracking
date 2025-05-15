using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Err.ChangeTracking.SourceGenerator.Extensions;

/// <summary>
///     Extension methods for Roslyn types
/// </summary>
internal static class RoslynExtensions
{
    /// <summary>
    ///     Check if a property is partial
    /// </summary>
    public static bool IsPartial(this IPropertySymbol propertySymbol)
    {
        foreach (var declaration in propertySymbol.DeclaringSyntaxReferences)
        {
            var syntax = declaration.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propertyDecl &&
                propertyDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                return true;
        }

        return false;
    }
}