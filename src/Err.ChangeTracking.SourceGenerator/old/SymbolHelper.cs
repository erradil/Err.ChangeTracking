/*using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

internal static class SymbolHelper0
{
    /// <summary>
    ///     Check if a property has a specific attribute
    /// </summary>
    public static bool HasAttribute(IPropertySymbol propertySymbol, string attributeFullName)
    {
        foreach (var attribute in propertySymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
                return true;

        return false;
    }


    /// <summary>
    ///     Check if a type is already a trackable collection
    /// </summary>
    public static bool IsTrackableCollection(IPropertySymbol propertySymbol)
    {
        var typeName = propertySymbol.Type.ToDisplayString();

        // No need to track the types is TrackableList or TrackableDictionary, they are by default trackable
        if (typeName.StartsWith(Constants.TrackableListFullName) ||
            typeName.StartsWith(Constants.TrackableDictionaryFullName))
            return false;
        // Check if the property has TrackCollectionAttribute
        var isTrackCollection = HasAttribute(propertySymbol, Constants.TrackCollectionAttributeFullName);

        return isTrackCollection
               && (typeName.StartsWith("System.Collections.Generic.List<T>") ||
                   typeName.StartsWith("System.Collections.Generic.Dictionary<TKey, TValue>"));
    }


    /// <summary>
    ///     Check if a type implements ITrackable<T> interface
    /// </summary>
    public static bool ImplementsTrackableInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            if (interfaceSymbol.OriginalDefinition.ToDisplayString().StartsWith(Constants.ITrackableFullName))
                return true;

        return false;
    }
}*/

