/*using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
///     Represents metadata about a type needed for generation
/// </summary>
internal class TypeInfo
{
    public string Name { get; }
    public string? Namespace { get; }
    public string Kind { get; } // class, struct, record, record struct
    public Accessibility Accessibility { get; }
    public List<string> Modifiers { get; } = [];
    public List<ContainingTypeInfo> ContainingTypeInfos { get; } = [];

    public TypeInfo(INamedTypeSymbol typeSymbol)
    {
        Name = typeSymbol.Name;
        Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();
        Kind = DetermineTypeKind(typeSymbol);
        Accessibility = typeSymbol.DeclaredAccessibility;

        // Extract modifiers
        if (typeSymbol.IsStatic) Modifiers.Add("static");
        if (typeSymbol.IsAbstract && typeSymbol.TypeKind != TypeKind.Interface) Modifiers.Add("abstract");
        if (typeSymbol.IsSealed && !typeSymbol.IsValueType && !typeSymbol.IsRecord) Modifiers.Add("sealed");

        // Get containing types
        var current = typeSymbol.ContainingType;
        while (current != null)
        {
            var containingTypeInfo = new ContainingTypeInfo(
                current.Name,
                DetermineTypeKind(current),
                current.DeclaredAccessibility,
                current.IsStatic,
                current is { IsAbstract: true, TypeKind: not TypeKind.Interface },
                current is { IsSealed: true, IsValueType: false, IsRecord: false }
            );

            ContainingTypeInfos.Insert(0, containingTypeInfo);
            current = current.ContainingType;
        }
    }

    /// <summary>
    ///     Determine the kind of a type (class, struct, record, record struct)
    /// </summary>
    private static string DetermineTypeKind(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsValueType) return typeSymbol.IsRecord ? "record struct" : "struct";

        return typeSymbol.IsRecord ? "record" : "class";
    }

    /// <summary>
    ///     Returns the fully qualified name of the type including namespace and any containing types
    /// </summary>
    public string GetFullName()
    {
        var sb = new StringBuilder();

        // Start with the namespace if not global
        if (!string.IsNullOrEmpty(Namespace)) sb.Append(Namespace);

        // Add all containing types in order
        foreach (var containingTypeInfo in ContainingTypeInfos)
        {
            if (sb.Length > 0)
                sb.Append('.');
            sb.Append(containingTypeInfo.Name);
        }

        // Add the type name itself
        if (sb.Length > 0)
            sb.Append('.');
        sb.Append(Name);

        return sb.ToString();
    }
}

/// <summary>
///     Information about a containing/parent type
/// </summary>
internal class ContainingTypeInfo
{
    public string Name { get; }
    public string Kind { get; }
    public Accessibility Accessibility { get; }
    public bool IsStatic { get; }
    public bool IsAbstract { get; }
    public bool IsSealed { get; }
    public List<string> Modifiers { get; } = [];

    public ContainingTypeInfo(string name, string kind, Accessibility accessibility, bool isStatic,
        bool isAbstract, bool isSealed)
    {
        Name = name;
        Kind = kind;
        Accessibility = accessibility;
        IsStatic = isStatic;
        IsAbstract = isAbstract;
        IsSealed = isSealed;

        // Build modifiers list
        if (isStatic) Modifiers.Add("static");
        if (isAbstract) Modifiers.Add("abstract");
        if (isSealed) Modifiers.Add("sealed");
    }
}

/// <summary>
///     Represents all metadata needed to generate a property implementation
/// </summary>
internal class PropertyInfo
{
    public string Name { get; }
    public string TypeName { get; }
    public string BackingFieldName { get; }
    public bool IsStatic { get; }
    public bool IsVirtual { get; }
    public bool IsOverride { get; }
    public bool IsAbstract { get; }
    public bool IsSealed { get; }
    public Accessibility PropertyAccessibility { get; }

    // Accessor information
    public bool HasGetter { get; }
    public bool HasSetter { get; }
    public bool IsSetterInitOnly { get; }
    public Accessibility GetterAccessibility { get; }
    public Accessibility SetterAccessibility { get; }

    // Collection tracking information
    public bool IsCollection { get; }
    public string? TrackableCollectionType { get; }
    public bool IsNullable { get; }

    public PropertyInfo(IPropertySymbol propertySymbol)
    {
        Name = propertySymbol.Name;
        TypeName = propertySymbol.Type.ToDisplayString();
        BackingFieldName = $"_{char.ToLowerInvariant(Name[0])}{Name.Substring(1)}";

        // Modifiers
        IsStatic = propertySymbol.IsStatic;
        IsVirtual = propertySymbol.IsVirtual;
        IsOverride = propertySymbol.IsOverride;
        IsAbstract = propertySymbol.IsAbstract;
        IsSealed = propertySymbol.IsSealed;
        PropertyAccessibility = propertySymbol.DeclaredAccessibility;

        // Accessors
        HasGetter = propertySymbol.GetMethod != null;
        HasSetter = propertySymbol.SetMethod != null;
        IsSetterInitOnly = HasSetter && propertySymbol.SetMethod!.IsInitOnly;
        GetterAccessibility =
            HasGetter ? propertySymbol.GetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
        SetterAccessibility =
            HasSetter ? propertySymbol.SetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
        // Check if the type is nullable
        IsNullable = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated;

        // Check if this is a trackable collection
        var collectionInfo = GetTrackableCollectionInfo(propertySymbol);
        IsCollection = collectionInfo.IsCollection;
        TrackableCollectionType = collectionInfo.TrackableType;
    }


    /// <summary>
    ///     Check if a property has the [TrackCollection] attribute
    /// </summary>
    private static bool HasTrackCollectionAttribute(IPropertySymbol propertySymbol)
    {
        foreach (var attribute in propertySymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == Constants.TrackCollectionAttributeFullName)
                return true;

        return false;
    }

    /// <summary>
    ///     Check if a property type is a collection that should be converted to a trackable version
    /// </summary>
    private static (bool IsCollection, string TrackableType) GetTrackableCollectionInfo(
        IPropertySymbol property)
    {
        if (!HasTrackCollectionAttribute(property))
            return (false, string.Empty);

        var propertyType = property.Type;


        // Check for List<T>
        if (propertyType is INamedTypeSymbol listType &&
            listType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
        {
            var elementType = listType.TypeArguments[0].ToDisplayString();
            return (true, $"{Constants.TrackableListFullName}<{elementType}>");
        }

        // Check for Dictionary<K,V>
        if (propertyType is INamedTypeSymbol dictType &&
            dictType.OriginalDefinition.ToDisplayString() ==
            "System.Collections.Generic.Dictionary<TKey, TValue>")
        {
            var keyType = dictType.TypeArguments[0].ToDisplayString();
            var valueType = dictType.TypeArguments[1].ToDisplayString();
            return (true, $"{Constants.TrackableDictionaryFullName}<{keyType}, {valueType}>");
        }

        return (false, string.Empty);
    }
}*/

