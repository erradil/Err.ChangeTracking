using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

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
    public static bool IsTrackableCollection(IPropertySymbol propertySymbol)
    {
        var typeName = propertySymbol.Type.ToDisplayString();

        // No need to track if already a trackable type
        if (typeName.StartsWith(Constants.TrackableListFullName) ||
            typeName.StartsWith(Constants.TrackableDictionaryFullName))
            return false;

        // Check if the property has TrackCollectionAttribute
        var isTrackCollection = HasAttribute(propertySymbol, Constants.TrackCollectionAttributeFullName);

        return isTrackCollection && IsCollectionType(propertySymbol.Type);
    }

    /// <summary>
    ///     Check if a type is a supported collection type
    /// </summary>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var originalType = namedType.OriginalDefinition.ToDisplayString();
        return originalType == "System.Collections.Generic.List<T>" ||
               originalType == "System.Collections.Generic.Dictionary<TKey, TValue>";
    }

    /// <summary>
    /// Check if a type implements ITrackable<T> interface
    /// </summary>
    public static bool ImplementsTrackableInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            if (interfaceSymbol.OriginalDefinition.ToDisplayString().StartsWith(Constants.ITrackableFullName))
                return true;

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
            if (attribute.AttributeClass?.ToDisplayString() == Constants.TrackableAttributeFullName)
            {
                // Check if attribute has constructor arguments for tracking mode
                if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Mode").Value.Value is int
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
}