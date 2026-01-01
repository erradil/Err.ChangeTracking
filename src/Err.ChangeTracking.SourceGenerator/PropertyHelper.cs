using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Err.ChangeTracking.SourceGenerator;

internal class PropertyHelper(IPropertySymbol propertySymbol)
{
    private readonly IPropertySymbol _propertySymbol = propertySymbol;

    /// <summary>
    ///     Gets the name of the property
    /// </summary>
    public string Name => _propertySymbol.Name;

    /// <summary>
    ///     Gets the type name of the property
    /// </summary>
    public string TypeName => _propertySymbol.Type.ToDisplayString();

    /// <summary>
    ///     Gets the backing field name for the property
    /// </summary>
    public string BackingFieldName =>
        $"_{char.ToLowerInvariant(_propertySymbol.Name[0])}{_propertySymbol.Name.Substring(1)}";

    /// <summary>
    ///     Gets whether the property is static
    /// </summary>
    public bool IsStatic => _propertySymbol.IsStatic;

    /// <summary>
    ///     Gets whether the property is virtual
    /// </summary>
    public bool IsVirtual => _propertySymbol.IsVirtual;

    /// <summary>
    ///     Gets whether the property is an override
    /// </summary>
    public bool IsOverride => _propertySymbol.IsOverride;

    /// <summary>
    ///     Gets whether the property is abstract
    /// </summary>
    public bool IsAbstract => _propertySymbol.IsAbstract;

    /// <summary>
    ///     Gets whether the property is sealed
    /// </summary>
    public bool IsSealed => _propertySymbol.IsSealed;

    /// <summary>
    ///     Gets the property accessibility
    /// </summary>
    public Accessibility PropertyAccessibility => _propertySymbol.DeclaredAccessibility;

    /// <summary>
    ///     Gets whether the property has a getter
    /// </summary>
    public bool HasGetter => _propertySymbol.GetMethod != null;

    /// <summary>
    ///     Gets whether the property has a setter
    /// </summary>
    public bool HasSetter => _propertySymbol.SetMethod != null;

    /// <summary>
    ///     Gets whether the setter is init-only
    /// </summary>
    public bool IsSetterInitOnly => _propertySymbol.SetMethod?.IsInitOnly ?? false;

    /// <summary>
    ///     Gets the accessibility of the getter
    /// </summary>
    public Accessibility GetterAccessibility =>
        _propertySymbol.GetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable;

    /// <summary>
    ///     Gets the accessibility of the setter
    /// </summary>
    public Accessibility SetterAccessibility =>
        _propertySymbol.SetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable;

    /// <summary>
    ///     Gets whether the property is nullable
    /// </summary>
    public bool IsNullable => _propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated;

    /// <summary>
    ///     Check if the property has a specific attribute
    /// </summary>
    public bool HasAttribute(string attributeFullName)
    {
        foreach (var attribute in _propertySymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
                return true;

        return false;
    }

    /// <summary>
    ///     Check if the property has [TrackOnly] attribute
    /// </summary>
    public bool HasTrackOnlyAttribute => HasAttribute(Constants.Types.TrackOnlyAttributeFullName);

    /// <summary>
    ///     Check if the property has [NotTracked] attribute
    /// </summary>
    public bool HasNotTrackedAttribute => HasAttribute(Constants.Types.NotTrackedAttributeFullName);

    /// <summary>
    ///     Check if the property has [TrackCollection] attribute
    /// </summary>
    public bool HasTrackCollectionAttribute => HasAttribute(Constants.Types.TrackCollectionAttributeFullName);

    /// <summary>
    ///     Check if the property has [DeepTracking] attribute
    /// </summary>
    public bool HasDeepTrackingAttribute => HasAttribute(Constants.Types.DeepTrackingAttributeFullName);


    /// <summary>
    ///     Check if the property is already a trackable collection
    /// </summary>
    public (bool isTrackableCollection, string? collectionWrapperType) GetTrackableCollectionInfo()
    {
        if (_propertySymbol.Type is not INamedTypeSymbol namedType)
            return (false, null);

        var typeName = _propertySymbol.Type.OriginalDefinition.ToDisplayString();

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
        var isTrackableCollection = HasAttribute(Constants.Types.TrackCollectionAttributeFullName);
        if (!isTrackableCollection)
            return (false, null);

        var genericArgs = string.Join(", ",
            namedType.TypeArguments.Select(t => t.OriginalDefinition.ToDisplayString()).ToList());
        return (isTrackableCollection, $"{ReplacePattern(collectionWrapperType, @"(\w+)<.*>", $"$1<{genericArgs}>")}");
    }

    /// <summary>
    ///     Check if the property is already a trackable collection type
    /// </summary>
    public bool IsAlreadyTrackableCollection =>
        IsTypeOf(Constants.Types.TrackableListFullName) ||
        IsTypeOf(Constants.Types.TrackableDictionaryFullName);

    /// <summary>
    ///     Check if the property's type implements ITrackable interface or has [Trackable] attribute
    /// </summary>
    public bool IsTypeImplementsTrackable =>
        _propertySymbol.Type is INamedTypeSymbol namedType &&
        (ImplementsInterface(namedType, Constants.Types.IAttachedTrackerFullName)
         || HasTrackableAttribute(namedType));

    /// <summary>
    ///     Check if the property is of a specific type
    /// </summary>
    public bool IsTypeOf(string typeFullName)
    {
        if (_propertySymbol.Type is not INamedTypeSymbol namedType)
            return false;

        var originalDef = namedType.OriginalDefinition.ToDisplayString();
        return originalDef == typeFullName;
    }

    /// <summary>
    ///     Check if the property is partial
    /// </summary>
    public bool IsPartial()
    {
        foreach (var declaration in _propertySymbol.DeclaringSyntaxReferences)
        {
            var syntax = declaration.GetSyntax();
            if (syntax is PropertyDeclarationSyntax propertyDecl &&
                propertyDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Get a PropertyInfo object for this property
    /// </summary>
    public PropertyInfo GetPropertyInfo()
    {
        var (isTrackableCollection, collectionWrapperType) = GetTrackableCollectionInfo();

        return new PropertyInfo
        {
            Name = Name,
            TypeName = TypeName,
            BackingFieldName = BackingFieldName,
            IsStatic = IsStatic,
            IsVirtual = IsVirtual,
            IsOverride = IsOverride,
            IsAbstract = IsAbstract,
            IsSealed = IsSealed,
            PropertyAccessibility = PropertyAccessibility,
            HasGetter = HasGetter,
            HasSetter = HasSetter,
            IsSetterInitOnly = IsSetterInitOnly,
            GetterAccessibility = GetterAccessibility,
            SetterAccessibility = SetterAccessibility,
            IsTrackableCollection = isTrackableCollection,
            CollectionWrapperType = collectionWrapperType,
            IsNullable = IsNullable,
            HasTrackOnlyAttribute = HasTrackOnlyAttribute,
            HasTrackCollectionAttribute = HasTrackCollectionAttribute,
            HasDeepTrackingAttribute = HasDeepTrackingAttribute,
            IsAlreadyTrackableCollection = IsAlreadyTrackableCollection,
            IsTypeImplementsTrackable = IsTypeImplementsTrackable
        };
    }

    /// <summary>
    ///     Checks if a type implements a specific interface
    /// </summary>
    private static bool ImplementsInterface(INamedTypeSymbol? typeSymbol, string interfaceFullName)
    {
        // Early return if type is null
        if (typeSymbol == null)
            return false;

        // Check if the type directly implements the interface
        foreach (var iface in typeSymbol.AllInterfaces)
            // For non-generic interfaces, just compare the full name
            if (iface.OriginalDefinition.ToDisplayString() == interfaceFullName)
                return true;

        // If this is a derived type, check its base type recursively
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            return ImplementsInterface(typeSymbol.BaseType, interfaceFullName);

        // No match found
        return false;
    }


    /// <summary>
    ///     Determines if the property is eligible to be tracked based on multiple criteria:
    ///     - Must be partial
    ///     - Must have a setter
    ///     - Must satisfy tracking mode requirements (TrackOnly for OnlyMarked mode, not NotTracked for All mode)
    /// </summary>
    /// <param name="containingType">The type containing this property</param>
    /// <returns>True if the property should be tracked, false otherwise</returns>
    public bool IsEligibleForTracking(INamedTypeSymbol containingType)
    {
        // Step 1: Quick return if property is already a trackable collection (no need to generate)
        if (IsAlreadyTrackableCollection)
            return false;

        // Step 2: Property must be partial
        if (!IsPartial())
            return false;

        // Step 3: Property must have a setter
        if (!HasSetter)
            return false;

        // Step 4: Check if container type has [Trackable] attribute
        if (!HasTrackableAttribute(containingType))
            return false;

        // Step 5: Check for [NotTracked] attribute (overrides all other settings)
        if (HasNotTrackedAttribute)
            return false;

        // Step 6: Check tracking mode
        var trackingMode = GetTrackingMode(containingType);

        // In OnlyMarked mode, the property must have [TrackOnly] attribute
        if (trackingMode == TrackingMode.OnlyMarked && !HasTrackOnlyAttribute)
            return false;

        // If we reached here, the property is eligible for tracking
        return true;
    }

    /// <summary>
    ///     Checks if the containing type has the [Trackable] attribute
    /// </summary>
    public static bool HasTrackableAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == Constants.Types.TrackableAttributeFullName)
                return true;

        return false;
    }

    /// <summary>
    ///     Gets the tracking mode from the [Trackable] attribute on the containing type
    /// </summary>
    public static TrackingMode GetTrackingMode(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
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

    private static string? ReplacePattern(string? input, string pattern, string replacement)
    {
        return input == null
            ? null
            : Regex.Replace(input, pattern, replacement);
    }
}