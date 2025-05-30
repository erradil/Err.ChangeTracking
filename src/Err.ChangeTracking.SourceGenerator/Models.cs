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

/// <summary>
///     Information about a type for code generation
/// </summary>
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
}

/// <summary>
///     Contains all information about a property needed for change tracking generation
/// </summary>
internal record struct PropertyInfo
{
    /// <summary>
    ///     Name of the property
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Type name of the property
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    ///     Name of the backing field
    /// </summary>
    public string BackingFieldName { get; set; }

    /// <summary>
    ///     Whether the property is static
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    ///     Whether the property is virtual
    /// </summary>
    public bool IsVirtual { get; set; }

    /// <summary>
    ///     Whether the property is an override
    /// </summary>
    public bool IsOverride { get; set; }

    /// <summary>
    ///     Whether the property is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    ///     Whether the property is sealed
    /// </summary>
    public bool IsSealed { get; set; }

    /// <summary>
    ///     Accessibility of the property
    /// </summary>
    public Accessibility PropertyAccessibility { get; set; }

    /// <summary>
    ///     Whether the property has a getter
    /// </summary>
    public bool HasGetter { get; set; }

    /// <summary>
    ///     Whether the property has a setter
    /// </summary>
    public bool HasSetter { get; set; }

    /// <summary>
    ///     Whether the setter is init-only
    /// </summary>
    public bool IsSetterInitOnly { get; set; }

    /// <summary>
    ///     Accessibility of the getter
    /// </summary>
    public Accessibility GetterAccessibility { get; set; }

    /// <summary>
    ///     Accessibility of the setter
    /// </summary>
    public Accessibility SetterAccessibility { get; set; }

    /// <summary>
    ///     Whether the property is a trackable collection
    /// </summary>
    public bool IsTrackableCollection { get; set; }

    /// <summary>
    ///     The collection wrapper type for collections
    /// </summary>
    public string? CollectionWrapperType { get; set; }

    /// <summary>
    ///     Whether the property type is nullable
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    ///     Whether the property has the TrackOnly attribute
    /// </summary>
    public bool HasTrackOnlyAttribute { get; set; }

    /// <summary>
    ///     Whether the property has the TrackCollection attribute
    /// </summary>
    public bool HasTrackCollectionAttribute { get; set; }

    /// <summary>
    ///     Whether the property has the DeepTracking attribute
    /// </summary>
    public bool HasDeepTrackingAttribute { get; set; }

    /// <summary>
    ///     Whether the property type is already a trackable collection
    /// </summary>
    public bool IsAlreadyTrackableCollection { get; set; }

    /// <summary>
    ///     Whether the property type implements ITrackable
    /// </summary>
    public bool IsTypeImplementsTrackable { get; set; }
}