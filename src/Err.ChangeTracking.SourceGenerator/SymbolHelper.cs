using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Err.ChangeTracking.SourceGenerator;

internal static class SymbolHelper
{
    /// <summary>
    /// Check if a symbol has a specific attribute
    /// </summary>
    public static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        foreach (var attribute in symbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
                return true;

        return false;
    }

    /// <summary>
    /// Check if a type is already a trackable collection
    /// </summary>
    public static (bool isTrackableCollection, string? collectionWrapperType) IsTrackableCollection(
        IPropertySymbol? propertySymbol)
    {
        if (propertySymbol?.Type is not INamedTypeSymbol namedType)
            return (false, null);

        var typeName = propertySymbol.Type.OriginalDefinition.ToDisplayString();

        // No need to track if already a trackable type
        if (typeName.StartsWith(Constants.Types.TrackableListFullName) ||
            typeName.StartsWith(Constants.Types.TrackableDictionaryFullName))
            return (true, null);

        var (isCollection, collectionWrapperType) = typeName switch
        {
            "System.Collections.Generic.List<T>" =>
                (isCollection: true, collectionWrapperType: Constants.Types.TrackableListFullName),

            "System.Collections.Generic.Dictionary<TKey, TValue>" =>
                (isCollection: true, collectionWrapperType: Constants.Types.TrackableDictionaryFullName),

            _ => (isCollection: false, collectionWrapperType: null)
        };

        // Check if the property is a Collection, now only List<> and Dictionary<> are supported
        if (!isCollection)
            return (false, null);

        // Check if the property has a TrackCollectionAttribute
        var isTrackableCollection = HasAttribute(propertySymbol, Constants.Types.TrackCollectionAttributeFullName);
        if (!isTrackableCollection)
            return (false, null);

        var genericArgs = string.Join(", ",
            namedType.TypeArguments.Select(t => t.OriginalDefinition.ToDisplayString()).ToList());
        return (isTrackableCollection, $"{collectionWrapperType?.ReplacePattern(@"(\w+)<.*>", $"$1<{genericArgs}>")}");
    }


    /// <summary>
    /// Checks if a type implements a specific interface (either directly or through inheritance)
    /// </summary>
    public static bool ImplementsInterface(INamedTypeSymbol? typeSymbol, string interfaceFullName)
    {
        // Early return if type is null
        if (typeSymbol == null)
            return false;

        // Check if the type directly implements the interface
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            // For non-generic interfaces, just compare the full name
            if (iface.OriginalDefinition.ToDisplayString() == interfaceFullName)
                return true;
        }

        // If this is a derived type, check its base type recursively
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            return ImplementsInterface(typeSymbol.BaseType, interfaceFullName);

        // No match found
        return false;
    }

    /// <summary>
    ///     Extracts containing type info from a type symbol
    /// </summary>
    public static IReadOnlyList<ContainingTypeInfo> ExtractContainingTypeInfos(INamedTypeSymbol typeSymbol)
    {
        var containingTypes = new List<ContainingTypeInfo>();
        var current = typeSymbol.ContainingType;

        while (current != null)
        {
            containingTypes.Insert(0, new ContainingTypeInfo(
                current.Name,
                DetermineTypeKind(current),
                current.DeclaredAccessibility,
                GetTypeModifiers(current)
            ));

            current = current.ContainingType;
        }

        return containingTypes;
    }

    /// <summary>
    ///     Determine the kind of a type (class, struct, record, record struct)
    /// </summary>
    public static string DetermineTypeKind(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsValueType)
            return typeSymbol.IsRecord ? "record struct" : "struct";

        return typeSymbol.IsRecord ? "record" : "class";
    }

    /// <summary>
    ///     Extract modifiers from a type symbol
    /// </summary>
    public static IReadOnlyList<string> GetTypeModifiers(INamedTypeSymbol typeSymbol)
    {
        var modifiers = new List<string>();

        if (typeSymbol.IsStatic)
            modifiers.Add("static");

        if (typeSymbol.IsAbstract && typeSymbol.TypeKind != TypeKind.Interface)
            modifiers.Add("abstract");

        if (typeSymbol.IsSealed && !typeSymbol.IsValueType && !typeSymbol.IsRecord)
            modifiers.Add("sealed");

        return modifiers;
    }

    /// <summary>
    ///     Get tracking mode from the [Trackable] attribute
    /// </summary>
    public static TrackingMode GetTrackingMode(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == Constants.Types.TrackableAttributeFullName)
            {
                // Check if attribute has constructor arguments for tracking mode
                if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == nameof(TrackableAttribute.Mode)).Value
                        .Value is int
                    trackingModeValue)
                    return (TrackingMode)trackingModeValue;

                break;
            }

        return TrackingMode.All; // Default if not specified
    }

    /// <summary>
    ///     Convert Accessibility enum to string representation
    /// </summary>
    public static string GetAccessibilityAsString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => string.Empty // Default or NotApplicable
        };
    }


    public static bool IsTypeOf(IPropertySymbol propertySymbol, string typeFullName)
    {
        var propertyType = propertySymbol.Type;

        if (propertyType is not INamedTypeSymbol namedType)
            return false;
        var fullTypeName = namedType.ToDisplayString();
        var namespaceName = namedType.ContainingNamespace.ToDisplayString();
        var originalDef = namedType.OriginalDefinition.ToDisplayString();
        return originalDef == typeFullName;
    }


    /// <summary>
    ///     Check if a property is partial
    /// </summary>
    public static bool IsPartial(IPropertySymbol propertySymbol)
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

    public static string ReplacePattern(this string input, string pattern, string replacement)
    {
        return Regex.Replace(input, pattern, replacement);
    }
}