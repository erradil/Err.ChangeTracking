using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
///     Helper class for working with type symbols and generating type-related code
/// </summary>
internal class TypeHelper
{
    private readonly INamedTypeSymbol _typeSymbol;

    public TypeHelper(INamedTypeSymbol typeSymbol)
    {
        _typeSymbol = typeSymbol;
    }

    /// <summary>
    ///     Creates a TypeInfo object from the type symbol
    /// </summary>
    /// <returns>TypeInfo object with all relevant type information</returns>
    public TypeInfo GetTypeInfo()
    {
        var properties = GetTrackableProperties();

        return new TypeInfo
        {
            Name = _typeSymbol.Name,
            Namespace = _typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : _typeSymbol.ContainingNamespace.ToDisplayString(),
            Kind = DetermineTypeKind(),
            Accessibility = _typeSymbol.DeclaredAccessibility,
            Modifiers = GetTypeModifiers(),
            ContainingTypeInfos = ExtractContainingTypeInfos(),
            AlreadyImplementsTrackable = ImplementsInterface(Constants.Types.ITrackableFullName),
            Properties = properties
        };
    }

    /// <summary>
    ///     Determines the kind of type (class, record, struct, record struct)
    /// </summary>
    private string DetermineTypeKind()
    {
        if (_typeSymbol.IsRecord) return _typeSymbol.IsValueType ? "record struct" : "record";

        return _typeSymbol.IsValueType ? "struct" : "class";
    }

    /// <summary>
    ///     Gets all modifiers for the type except access modifiers and partial
    /// </summary>
    private IReadOnlyList<string> GetTypeModifiers()
    {
        var modifiers = new List<string>();

        if (_typeSymbol.IsStatic)
            modifiers.Add("static");
        if (_typeSymbol.IsAbstract && !_typeSymbol.IsStatic)
            modifiers.Add("abstract");
        if (_typeSymbol.IsSealed && !_typeSymbol.IsStatic && !_typeSymbol.IsValueType && !_typeSymbol.IsRecord)
            modifiers.Add("sealed");

        return modifiers;
    }

    /// <summary>
    ///     Extracts information about all containing/parent types
    /// </summary>
    private IReadOnlyList<ContainingTypeInfo> ExtractContainingTypeInfos()
    {
        var containingTypes = new List<ContainingTypeInfo>();
        var currentType = _typeSymbol.ContainingType;

        while (currentType != null)
        {
            var helper = new TypeHelper(currentType);
            containingTypes.Add(new ContainingTypeInfo(
                currentType.Name,
                helper.DetermineTypeKind(),
                currentType.DeclaredAccessibility,
                helper.GetTypeModifiers()
            ));

            currentType = currentType.ContainingType;
        }

        // We collected types from innermost to outermost, but need to display from outermost to innermost
        containingTypes.Reverse();
        return containingTypes;
    }

    /// <summary>
    ///     Checks if the type implements a specific interface
    /// </summary>
    private bool ImplementsInterface(string interfaceFullName)
    {
        // Check direct interface implementations
        foreach (var iface in _typeSymbol.AllInterfaces)
            if (iface.OriginalDefinition.ToDisplayString() == interfaceFullName)
                return true;

        return false;
    }

    /// <summary>
    ///     Gets tracking mode from the [Trackable] attribute on the type
    /// </summary>
    public TrackingMode GetTrackingMode()
    {
        foreach (var attribute in _typeSymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == Constants.Types.TrackableAttributeFullName)
            {
                // Check named arguments
                if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == nameof(TrackableAttribute.Mode)).Value
                        .Value is int namedValue)
                    return (TrackingMode)namedValue;

                // Check constructor arguments
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var value = attribute.ConstructorArguments[0].Value;
                    if (value is int trackingModeValue)
                        return (TrackingMode)trackingModeValue;
                }

                break;
            }

        return TrackingMode.All; // Default if not specified
    }

    /// <summary>
    ///     Gets all properties that should be tracked based on tracking mode and attributes
    /// </summary>
    private ImmutableArray<PropertyInfo> GetTrackableProperties()
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();
        var trackingMode = GetTrackingMode();

        foreach (var member in _typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var propertyHelper = new PropertyHelper(member);

            // Skip non-partial properties unless they're already trackable collections
            if (!propertyHelper.IsPartial() && !propertyHelper.IsAlreadyTrackableCollection)
                continue;

            // skip for init-only setter
            if (propertyHelper.IsSetterInitOnly)
                continue;

            // If explicitly marked not to track, then don't track
            if (propertyHelper.HasNotTrackedAttribute)
                continue;

            // If mode is OnlyMarked, then only properties with [TrackOnly] are tracked
            if (trackingMode == TrackingMode.OnlyMarked && !propertyHelper.HasTrackOnlyAttribute)
                continue;

            // Add property to tracked properties
            properties.Add(propertyHelper.GetPropertyInfo());
        }

        return properties.ToImmutable();
    }
}