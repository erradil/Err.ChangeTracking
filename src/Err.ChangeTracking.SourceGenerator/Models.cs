using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
/// Information about a containing/parent type
/// </summary>
internal record struct ContainingTypeInfo
{
    public string Name { get; init; }
    public string Kind { get; init; }
    public Accessibility Accessibility { get; init; }
    public IReadOnlyList<string> Modifiers { get; init; }

    public ContainingTypeInfo(string name, string kind, Accessibility accessibility, IReadOnlyList<string> modifiers)
    {
        Name = name;
        Kind = kind;
        Accessibility = accessibility;
        Modifiers = modifiers;
    }
}

internal record struct TypeInfo
{
    /// <summary>
    ///     Name of the type
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///     Namespace of the type
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    ///     Kind of the type (class, struct, record, record struct)
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    ///     Accessibility of the type
    /// </summary>
    public Accessibility Accessibility { get; init; }

    /// <summary>
    ///     Modifiers of the type
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; init; }

    /// <summary>
    ///     Information about containing types
    /// </summary>
    public IReadOnlyList<ContainingTypeInfo> ContainingTypeInfos { get; init; }

    /// <summary>
    ///     Whether the type already implements ITrackable interface
    /// </summary>
    public bool? AlreadyImplementsTrackable { get; init; }

    /// <summary>
    ///     Trackable properties
    /// </summary>
    public ImmutableArray<PropertyInfo> Properties { get; init; }

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
    public string Name { get; init; }
    public string TypeName { get; init; }
    public string BackingFieldName { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsSealed { get; init; }
    public Accessibility PropertyAccessibility { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public bool IsSetterInitOnly { get; init; }
    public Accessibility GetterAccessibility { get; init; }
    public Accessibility SetterAccessibility { get; init; }
    public bool IsCollection { get; init; }
    public string? TrackableCollectionType { get; init; }
    public bool IsNullable { get; init; }
}