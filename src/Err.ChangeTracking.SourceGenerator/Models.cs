using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
/// Information about a containing/parent type
/// </summary>
internal record struct ContainingTypeInfo
{
    public string Name { get; set; }
    public string Kind { get; set; }
    public Accessibility Accessibility { get; set; }
    public IReadOnlyList<string> Modifiers { get; set; }

    public ContainingTypeInfo(string name, string kind, Accessibility accessibility, IReadOnlyList<string> modifiers)
    {
        Name = name;
        Kind = kind;
        Accessibility = accessibility;
        Modifiers = modifiers;
    }
}

/// <summary>
///     Represents metadata about a type needed for generation
/// </summary>
internal record struct TypeInfo
{
    public string Name { get; set; }
    public string? Namespace { get; set; }
    public string Kind { get; set; }
    public Accessibility Accessibility { get; set; }
    public IReadOnlyList<string> Modifiers { get; set; }
    public IReadOnlyList<ContainingTypeInfo> ContainingTypeInfos { get; set; }

    public TypeInfo(string name, string? @namespace, string kind, Accessibility accessibility,
        IReadOnlyList<string> modifiers, IReadOnlyList<ContainingTypeInfo> containingTypeInfos)
    {
        Name = name;
        Namespace = @namespace;
        Kind = kind;
        Accessibility = accessibility;
        Modifiers = modifiers;
        ContainingTypeInfos = containingTypeInfos;
    }

    /// <summary>
    /// Returns the fully qualified name of the type including namespace and any containing types
    /// </summary>
    public string GetFullName()
    {
        var parts = new List<string>();

        // Add namespace if not global
        if (!string.IsNullOrEmpty(Namespace))
            parts.Add(Namespace);

        // Add containing types
        foreach (var containingType in ContainingTypeInfos)
            parts.Add(containingType.Name);

        // Add the type name
        parts.Add(Name);

        return string.Join(".", parts);
    }
}

/// <summary>
/// Represents all metadata needed to generate a property implementation
/// </summary>
internal record struct PropertyInfo
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public string BackingFieldName { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public Accessibility PropertyAccessibility { get; set; }
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public bool IsSetterInitOnly { get; set; }
    public Accessibility GetterAccessibility { get; set; }
    public Accessibility SetterAccessibility { get; set; }
    public bool IsCollection { get; set; }
    public string? TrackableCollectionType { get; set; }
    public bool IsNullable { get; set; }

    public PropertyInfo(string name, string typeName, string backingFieldName, bool isStatic, bool isVirtual,
        bool isOverride, bool isAbstract, bool isSealed, Accessibility propertyAccessibility, bool hasGetter,
        bool hasSetter, bool isSetterInitOnly, Accessibility getterAccessibility,
        Accessibility setterAccessibility, bool isCollection, string? trackableCollectionType, bool isNullable)
    {
        Name = name;
        TypeName = typeName;
        BackingFieldName = backingFieldName;
        IsStatic = isStatic;
        IsVirtual = isVirtual;
        IsOverride = isOverride;
        IsAbstract = isAbstract;
        IsSealed = isSealed;
        PropertyAccessibility = propertyAccessibility;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        IsSetterInitOnly = isSetterInitOnly;
        GetterAccessibility = getterAccessibility;
        SetterAccessibility = setterAccessibility;
        IsCollection = isCollection;
        TrackableCollectionType = trackableCollectionType;
        IsNullable = isNullable;
    }
}