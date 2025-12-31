using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
///     Helper class for generating type-related code
/// </summary>
internal class TypeDisplayHelper
{
    private readonly TypeInfo _typeInfo;

    public TypeDisplayHelper(TypeInfo typeInfo)
    {
        _typeInfo = typeInfo;
    }

    /// <summary>
    ///     Returns the fully qualified name of the type including namespace and any containing types
    /// </summary>
    public string GetFullName()
    {
        var parts = new List<string>();

        // Add namespace if not global
        if (!string.IsNullOrEmpty(_typeInfo.Namespace))
            parts.Add(_typeInfo.Namespace!);

        // Add containing types
        foreach (var containingType in _typeInfo.ContainingTypeInfos)
            parts.Add(containingType.Name);

        // Add the type name
        parts.Add(_typeInfo.Name);

        return string.Join(".", parts);
    }

    /// <summary>
    ///     Returns the type declaration with all modifiers and interface implementation
    /// </summary>
    /// <param name="implementInterface">Whether to include the ITrackable interface in the declaration</param>
    public string ToDisplayDeclaration(bool implementInterface = true)
    {
        var accessibility = GetAccessibilityString(_typeInfo.Accessibility);
        var modifiers = string.Join(" ", _typeInfo.Modifiers);
        var modifiersWithSpace = modifiers.Length > 0 ? $"{modifiers} " : "";

        // Add the ITrackable<T> interface if requested and not already implemented
        var interfaceImplementation = implementInterface && _typeInfo.AlreadyImplementsTrackable is false
            ? $" : {Constants.Types.ITrackableFullName.Replace("<TEntity>", $"<{GetFullName()}>")}, {Constants.Types.IAttachedTrackerFullName.Replace("<TEntity>", $"<{GetFullName()}>")}"
            : "";

        return
            $"{accessibility} {modifiersWithSpace}partial {_typeInfo.Kind} {_typeInfo.Name}{interfaceImplementation}";
    }

    /// <summary>
    ///     Returns the static constructor for deep change tracking
    /// </summary>
    public string ToDisplayStaticConstructor(string indent = "")
    {
        // Get all properties that are either trackable entities or collections
        var deepTrackingProperties = _typeInfo.Properties.Where(p =>
                p.HasDeepTrackingAttribute)
            .ToList();

        if (deepTrackingProperties.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine($"{indent}static {_typeInfo.Name}()");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    // Get all properties that are either deep trackable entities or collections");
        sb.AppendLine(
            $"{indent}    {Constants.Types.DeepTrackingFullName.Replace("<T>", $"<{_typeInfo.Name}>")}.SetTrackableProperties([");

        // Generate delegates for each trackable property
        foreach (var property in deepTrackingProperties)
        {
            var propertyDisplay = new PropertyDisplayHelper(property);
            sb.AppendLine($"{indent}        {propertyDisplay.ToDisplayDeepTrackingDelegate()}");
        }

        sb.AppendLine($"{indent}    ]);");
        sb.AppendLine($"{indent}}}");

        return sb.ToString();
    }
    
    /// <summary>
    ///     Convert an accessibility level to its string representation
    /// </summary>
    private static string GetAccessibilityString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => ""
        };
    }
}

/// <summary>
///     Helper class for generating containing type-related code
/// </summary>
internal class ContainingTypeDisplayHelper
{
    private readonly ContainingTypeInfo _containingTypeInfo;

    public ContainingTypeDisplayHelper(ContainingTypeInfo containingTypeInfo)
    {
        _containingTypeInfo = containingTypeInfo;
    }

    /// <summary>
    ///     Returns the containing type declaration
    /// </summary>
    public string ToDisplayDeclaration()
    {
        var accessibility = GetAccessibilityString(_containingTypeInfo.Accessibility);
        var modifiers = string.Join(" ", _containingTypeInfo.Modifiers);
        var modifiersWithSpace = modifiers.Length > 0 ? $"{modifiers} " : "";

        return $"{accessibility} {modifiersWithSpace}partial {_containingTypeInfo.Kind} {_containingTypeInfo.Name}";
    }

    /// <summary>
    ///     Convert an accessibility level to its string representation
    /// </summary>
    private static string GetAccessibilityString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => ""
        };
    }
}