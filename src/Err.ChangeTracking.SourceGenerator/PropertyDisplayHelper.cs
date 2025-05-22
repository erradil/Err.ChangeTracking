using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
///     Helper class for generating property-related code
/// </summary>
internal class PropertyDisplayHelper
{
    private readonly PropertyInfo _property;

    public PropertyDisplayHelper(PropertyInfo property)
    {
        _property = property;
    }

    /// <summary>
    ///     Returns the property declaration signature with modifiers
    /// </summary>
    /// <returns>Property declaration (e.g., "public virtual partial string Name")</returns>
    public string ToDisplaySignature()
    {
        var modifiers = new List<string>(7);

        // Add accessibility 
        modifiers.Add(GetAccessibilityString(_property.PropertyAccessibility));

        // Add other modifiers
        if (_property.IsStatic) modifiers.Add("static");
        if (_property.IsVirtual) modifiers.Add("virtual");
        if (_property.IsOverride) modifiers.Add("override");
        if (_property.IsSealed) modifiers.Add("sealed");
        if (_property.IsAbstract) modifiers.Add("abstract");
        modifiers.Add("partial");

        // Build the signature
        return $"{string.Join(" ", modifiers)} {_property.TypeName} {_property.Name}";
    }

    /// <summary>
    ///     Returns the backing field declaration for this property
    /// </summary>
    /// <returns>Backing field (e.g., "private string _name;")</returns>
    public string ToDisplayBackingField()
    {
        var staticModifier = _property.IsStatic ? "static " : "";
        var nullableAnnotation = _property.IsNullable ? "?" : "";
        var fieldType = _property.CollectionWrapperType is not null
            ? $"{_property.CollectionWrapperType}{nullableAnnotation}"
            : _property.TypeName;

        return $"private {staticModifier}{fieldType} {_property.BackingFieldName};";
    }

    /// <summary>
    ///     Returns the property getter expression
    /// </summary>
    /// <returns>Getter code (e.g., "get => _name;" or "get;")</returns>
    public string ToDisplayGetter()
    {
        var getterAccessibility = _property.GetterAccessibility != _property.PropertyAccessibility
            ? $"{GetAccessibilityString(_property.GetterAccessibility)} "
            : "";

        if (!_property.IsAbstract)
            return $"{getterAccessibility}get => {_property.BackingFieldName};";
        return $"{getterAccessibility}get;";
    }

    /// <summary>
    ///     Returns the property setter expression
    /// </summary>
    /// <returns>Setter code with change tracking logic</returns>
    public string ToDisplaySetter()
    {
        var setterAccessibility = _property.SetterAccessibility != _property.PropertyAccessibility
            ? $"{GetAccessibilityString(_property.SetterAccessibility)} "
            : "";

        var accessorKeyword = _property.IsSetterInitOnly ? "init" : "set";

        if (_property.IsAbstract)
            return $"{setterAccessibility}{accessorKeyword};";

        if (!_property.IsStatic)
            return
                $"{setterAccessibility}{accessorKeyword} {{ this.GetChangeTracker().RecordChange(nameof({_property.Name}), {_property.BackingFieldName}, value); {ToDisplayAssignment()} }}";

        return $"{setterAccessibility}{accessorKeyword} {{ {ToDisplayAssignment()} }}";
    }

    /// <summary>
    ///     Returns the assignment expression for the property setter
    /// </summary>
    /// <returns>Assignment code for the setter body</returns>
    public string ToDisplayAssignment()
    {
        if (_property.CollectionWrapperType is not null)
            return
                $"{_property.BackingFieldName} = value != null ? new {_property.CollectionWrapperType}(value) : null;";
        return $"{_property.BackingFieldName} = value;";
    }

    /// <summary>
    ///     Returns the complete property implementation
    /// </summary>
    /// <returns>Complete property implementation with backing field, getters and setters</returns>
    public string ToDisplayFullProperty(string indent = "")
    {
        var sb = new StringBuilder();

        // Comments explaining why property is tracked
        if (_property.HasTrackOnlyAttribute)
            sb.AppendLine($"{indent}// This property is tracked because it has the [TrackOnly] attribute");
        else
            sb.AppendLine($"{indent}// This property is tracked by default based on TrackingMode.All");

        if (_property.HasTrackCollectionAttribute)
            sb.AppendLine($"{indent}// Using trackable collection wrapper due to [TrackCollection] attribute");

        // Backing field
        sb.AppendLine($"{indent}{ToDisplayBackingField()}");

        // Property declaration and body
        sb.AppendLine($"{indent}{ToDisplaySignature()}");
        sb.AppendLine($"{indent}{{");

        // Accessors
        if (_property.HasGetter)
            sb.AppendLine($"{indent}    {ToDisplayGetter()}");

        if (_property.HasSetter)
            sb.AppendLine($"{indent}    {ToDisplaySetter()}");

        sb.AppendLine($"{indent}}}");

        return sb.ToString();
    }

    /// <summary>
    ///     Returns the track delegate expression for use in static constructor
    /// </summary>
    /// <returns>Delegate expression for deep change tracking</returns>
    public string ToDisplayTrackDelegate()
    {
        if (_property.IsTypeImplementsTrackable)
            return $"x => x.{_property.Name}?.GetChangeTracker()";
        if (_property.IsTrackableCollection || _property.IsAlreadyTrackableCollection)
            return $"x => x.{_property.Name} as IBaseTracking";
        return string.Empty;
    }

    /// <summary>
    ///     Returns a comment describing the property tracking status
    /// </summary>
    public string ToDisplayComment()
    {
        var sb = new StringBuilder();

        if (_property.HasTrackOnlyAttribute)
            sb.Append("This property is tracked because it has the [TrackOnly] attribute");
        else
            sb.Append("This property is tracked by default based on TrackingMode.All");

        if (_property.HasTrackCollectionAttribute)
            sb.Append(". Using trackable collection wrapper due to [TrackCollection] attribute");

        if (_property.IsTypeImplementsTrackable)
            sb.Append(". Property type implements ITrackable interface");

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