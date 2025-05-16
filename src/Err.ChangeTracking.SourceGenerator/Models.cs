using System.Collections.Generic;
using System.Collections.Immutable;
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

internal record struct TypeInfo
{
    /// <summary>
    ///     Name of the type
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Namespace of the type
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    ///     Kind of the type (class, struct, record, record struct)
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    ///     Accessibility of the type
    /// </summary>
    public Accessibility Accessibility { get; set; }

    /// <summary>
    ///     Modifiers of the type
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; set; }

    /// <summary>
    ///     Information about containing types
    /// </summary>
    public IReadOnlyList<ContainingTypeInfo> ContainingTypeInfos { get; set; }

    /// <summary>
    ///     Whether the type already implements ITrackable interface
    /// </summary>
    public bool? AlreadyImplementsTrackable { get; set; }

    /// <summary>
    ///     Trackable properties
    /// </summary>
    public ImmutableArray<PropertyInfo> Properties { get; set; }

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
/// Extension to PropertyInfo to include tracking-related attributes
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
    public bool IsTrackCollection { get; set; }
    public string? CollectionWrapperType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsTrackOnly { get; set; }
    public bool HasTrackCollection { get; set; }
}