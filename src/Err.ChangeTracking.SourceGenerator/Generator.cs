using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Err.ChangeTracking.SourceGenerator;

/// <summary>
///     Source generator that implements partial properties with change tracking
/// </summary>
[Generator]
public class ChangeTrackingGenerator : IIncrementalGenerator
{
    #region Initialization

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Set up the pipeline for the Trackable attribute
        var trackableTypes =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Constants.Types.TrackableAttributeFullName,
                    static (node, _) =>
                        node is TypeDeclarationSyntax typeDecl && HasPartialModifier(typeDecl),
                    static (ctx, _) => GetTypeInfo(ctx))
                .Where(static t => t is not null);

        // Register the source output
        context.RegisterSourceOutput(trackableTypes, static (ctx, typeInfo) => GenerateSource(ctx, typeInfo!.Value));
    }

    /// <summary>
    /// Check if the node has a partial modifier
    /// </summary>
    private static bool HasPartialModifier(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Transform the GeneratorAttributeSyntaxContext to get the type declaration and symbol
    /// </summary>
    private static TypeInfo? GetTypeInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx is not { TargetNode: TypeDeclarationSyntax typeDecl, TargetSymbol: INamedTypeSymbol typeSymbol })
        {
            return null;
        }

        // Extract properties that should be tracked
        var properties = GetTrackableProperties(typeSymbol);

        if (properties.IsEmpty)
            return null;

        // Create TypeInfo
        var typeInfo = new TypeInfo
        {
            Name = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            Kind = SymbolHelper.DetermineTypeKind(typeSymbol),
            Accessibility = typeSymbol.DeclaredAccessibility,
            Modifiers = SymbolHelper.GetTypeModifiers(typeSymbol),
            ContainingTypeInfos = SymbolHelper.ExtractContainingTypeInfos(typeSymbol),
            AlreadyImplementsTrackable = SymbolHelper.ImplementsTrackableInterface(typeSymbol),
            Properties = properties
        };

        return typeInfo;
    }

    /// <summary>
    ///     Get all properties that should be tracked based on attributes and tracking mode
    /// </summary>
    private static ImmutableArray<PropertyInfo> GetTrackableProperties(
        INamedTypeSymbol typeSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();

        // Get tracking mode from type
        var trackingMode = SymbolHelper.GetTrackingMode(typeSymbol);

        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            // Skip non-partial properties
            if (!member.IsPartial())
                continue;

            // Check property attributes
            var isTrackOnly = SymbolHelper.HasAttribute(member, Constants.Types.TrackOnlyAttributeFullName);
            var isNotTracked = SymbolHelper.HasAttribute(member, Constants.Types.NotTrackedAttributeFullName);
            var hasTrackCollection =
                SymbolHelper.HasAttribute(member, Constants.Types.TrackCollectionAttributeFullName);

            // If explicitly marked not to track, then don't track
            if (isNotTracked)
                continue;

            // If mode is OnlyMarked, then only properties with [TrackOnly] are tracked
            if (trackingMode == TrackingMode.OnlyMarked && !isTrackOnly)
                continue;

            // If the type is already TrackableList or TrackableDictionary, not need to track
            if (member.IsTypeOf(Constants.Types.TrackableDictionaryFullName) ||
                member.IsTypeOf(Constants.Types.TrackableListFullName))
                continue;

            // We've determined this property should be tracked, now check if it's a collection
            var (isTrackCollection, collectionWrapperType) = SymbolHelper.IsTrackableCollection(member);

            var trackableProperty = new PropertyInfo
            {
                Name = member.Name,
                TypeName = member.Type.ToDisplayString(),
                BackingFieldName = $"_{char.ToLowerInvariant(member.Name[0])}{member.Name.Substring(1)}",
                IsStatic = member.IsStatic,
                IsVirtual = member.IsVirtual,
                IsOverride = member.IsOverride,
                IsAbstract = member.IsAbstract,
                IsSealed = member.IsSealed,
                PropertyAccessibility = member.DeclaredAccessibility,
                HasGetter = member.GetMethod != null,
                HasSetter = member.SetMethod != null,
                IsSetterInitOnly = member.SetMethod?.IsInitOnly ?? false,
                GetterAccessibility = member.GetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable,
                SetterAccessibility = member.SetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable,
                IsTrackCollection = isTrackCollection,
                CollectionWrapperType = collectionWrapperType,
                IsNullable = member.Type.NullableAnnotation == NullableAnnotation.Annotated,
                IsTrackOnly = isTrackOnly,
                HasTrackCollection = hasTrackCollection
            };

            properties.Add(trackableProperty);
        }

        return properties.ToImmutable();
    }

    #endregion

    #region Main Generation

    /// <summary>
    /// Generate source code for a type
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>
    /// // <auto-generated>
    /// // This code was generated by the Err.ChangeTracking Source Generator
    /// // Changes to this file may cause incorrect behavior and will be lost if the code is regenerated
    /// // </auto-generated>
    /// 
    /// #nullable enable
    /// 
    /// namespace Err.ChangeTracking.SampleDemo.Models;
    /// 
    /// // Auto-generated implementation for Person
    /// public partial class Person : ITrackable&ltPerson&lg
    /// {
    ///     // ...generated content...
    /// }
    /// </code>
    /// </remarks>
    private static void GenerateSource(SourceProductionContext context, TypeInfo typeInfo)
    {
        var sourceBuilder = new StringBuilder(2048); // Pre-allocate a reasonable buffer size

        GenerateFileHeader(sourceBuilder);

        // Add namespace if needed
        if (typeInfo.Namespace != null)
        {
            sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};")
                .AppendLine();
        }

        // Generate the type (handles both nested and non-nested cases)
        GenerateType(sourceBuilder, typeInfo);

        var fileName = $"{typeInfo.GetFullName()}.g.cs";
        context.AddSource(fileName, SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    ///     Generate the file header with auto-generated comments and nullable enable directive
    /// </summary>
    /// <remarks>
    ///     Example output:
    ///     <code>
    /// // <auto-generated>
    ///             // This code was generated by the Err.ChangeTracking Source Generator
    ///             // Changes to this file may cause incorrect behavior and will be lost if the code is regenerated
    ///             //
    ///         </auto-generated>
    /// 
    /// #nullable enable
    /// </code>
    /// </remarks>
    private static void GenerateFileHeader(StringBuilder sourceBuilder)
    {
        sourceBuilder
            .AppendLine(
                """
                #pragma warning disable CS8601 // Possible null reference assignment.
                #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
                // <auto-generated>
                // This code was generated by the Err.ChangeTracking Source Generator
                // Changes to this file may cause incorrect behavior and will be lost if the code is regenerated
                // </auto-generated>
                #nullable enable
                """);
    }

    #endregion

    #region Type Generation

    /// <summary>
    /// Generate a type with its properties, handling both nested and non-nested types
    /// </summary>
    /// <remarks>
    /// Example output for a non-nested class:
    /// <code>
    /// // Auto-generated implementation for Person
    /// public partial class Person : ITrackable&lt;Person&gt;
    /// {
    ///     // ITrackable interface implementation
    ///     private IChangeTracking&lt;Person&gt;? _changeTracker;
    ///     public IChangeTracking&lt;Person&gt; GetChangeTracker() => _changeTracker ??= ChangeTracking.Create(this);
    ///     
    ///     // Property implementations...
    /// }
    /// </code>
    /// 
    /// Example output for a nested class:
    /// <code>
    /// public partial class OuterClass
    /// {
    ///     // Auto-generated implementation for InnerClass
    ///     public partial class InnerClass : ITrackable&lt;OuterClass.InnerClass&gt;
    ///     {
    ///         // ITrackable interface implementation
    ///         private IChangeTracking&lt;OuterClass.InnerClass&gt;? _changeTracker;
    ///         public IChangeTracking&lt;OuterClass.InnerClass&gt; GetChangeTracker() => 
    ///             _changeTracker ??= ChangeTracking.Create(this);
    ///         
    ///         // Property implementations...
    ///     }
    /// }
    /// </code>
    /// </remarks>
    private static void GenerateType(
        StringBuilder sourceBuilder,
        TypeInfo typeInfo)
    {
        // Indent management
        var indent = 0;

        // Get the fully qualified type name for interface implementation
        var fullTypeName = typeInfo.GetFullName();
        var typeName = typeInfo.ContainingTypeInfos.Count > 0
            ? fullTypeName
            : typeInfo.Name;

        // For nested types, generate the containing type hierarchy first
        if (typeInfo.ContainingTypeInfos.Count > 0)
        {
            // For each containing type, open a partial type declaration
            foreach (var containingType in typeInfo.ContainingTypeInfos)
            {
                var indentStr = GetIndentation(indent);

                var containingTypeDeclaration = BuildTypeDeclaration(
                    new TypeInfo
                    {
                        Name = containingType.Name,
                        Namespace = null,
                        Kind = containingType.Kind,
                        Accessibility = containingType.Accessibility,
                        Modifiers = containingType.Modifiers,
                        ContainingTypeInfos = new List<ContainingTypeInfo>(),
                        AlreadyImplementsTrackable = null
                    },
                    containingType.Name
                );

                sourceBuilder.AppendLine(
                        $"{indentStr}{containingTypeDeclaration.Replace($" : {Constants.Types.ITrackableFullName}<{containingType.Name}>", "")}")
                    .AppendLine($"{indentStr}{{");

                indent++;
            }
        }

        // Now generate the actual type
        var typeIndent = GetIndentation(indent);

        // Build type declaration with all modifiers
        var typeDeclaration = BuildTypeDeclaration(typeInfo, typeName);

        // Example of what's being generated here:
        // // Auto-generated implementation for Person
        // public partial class Person : ITrackable<Person>
        // {
        sourceBuilder.AppendLine($"{typeIndent}// Auto-generated implementation for {typeInfo.Name}")
            .AppendLine($"{typeIndent}{typeDeclaration}")
            .AppendLine($"{typeIndent}{{");
        indent++;

        // If we're adding the interface implementation, generate the required members
        if (typeInfo.AlreadyImplementsTrackable is false)
        {
            GenerateTrackingImplementation(sourceBuilder, typeName, GetIndentation(indent));
        }

        // Generate each property
        foreach (var property in typeInfo.Properties)
        {
            GenerateProperty(sourceBuilder, property, GetIndentation(indent));
        }

        // Close all type declarations (the current type + any containing types)
        for (var i = indent; i > 0; i--) sourceBuilder.AppendLine($"{GetIndentation(i - 1)}}}");
    }

    /// <summary>
    ///     Get indentation string for the specified level
    /// </summary>
    private static string GetIndentation(int level)
    {
        return new string(' ', level * 4);
    }

    /// <summary>
    /// Generate tracking implementation for a type
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>
    /// // ITrackable interface implementation
    /// private IChangeTracking&lt;Person&gt;? _changeTracker;
    /// public IChangeTracking&lt;Person&gt; GetChangeTracker() => _changeTracker ??= ChangeTracking.Create(this);
    /// </code>
    /// </remarks>
    private static void GenerateTrackingImplementation(StringBuilder sourceBuilder, string typeName, string indent)
    {
        sourceBuilder.AppendLine($"{indent}// ITrackable interface implementation")
            .AppendLine($"{indent}private {Constants.Types.IChangeTrackingFullName}<{typeName}>? _changeTracker;")
            .AppendLine(
                $"{indent}public {Constants.Types.IChangeTrackingFullName}<{typeName}> GetChangeTracker() => _changeTracker ??= {Constants.Types.ChangeTrackingFullName}.Create(this);")
            .AppendLine();
    }

    /// <summary>
    /// Build the type declaration with appropriate modifiers and interface implementation
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>
    /// public partial class Person : ITrackable&lt;Personl&gt;
    /// </code>
    /// </remarks>
    private static string BuildTypeDeclaration(TypeInfo typeInfo, string typeName)
    {
        var accessibility = SymbolHelper.GetAccessibilityAsString(typeInfo.Accessibility);
        var modifiers = string.Join(" ", typeInfo.Modifiers);
        var modifiersWithSpace = modifiers.Length > 0 ? $"{modifiers} " : "";

        // Add the ITrackable<T> interface if not already implemented
        var interfaceImplementation = typeInfo.AlreadyImplementsTrackable is false
            ? $" : {Constants.Types.ITrackableFullName}<{typeName}>"
            : "";

        return
            $"{accessibility} {modifiersWithSpace}{Constants.Keywords.PartialKeyword} {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}";
    }

    #endregion

    #region Property Generation

    /// <summary>
    /// Generate a property implementation with custom indentation
    /// </summary>
    /// <remarks>
    /// Example output for a standard property:
    /// <code>
    /// // This property is tracked by default based on TrackingMode.All
    /// private string _name;
    /// public partial string Name
    /// {
    ///     get => _name;
    ///     set { _changeTracker?.RecordChange(nameof(Name), _name, value); _name = value; }
    /// }
    /// </code>
    /// 
    /// Example output for a tracked collection:
    /// <code>
    /// // This property is tracked by default based on TrackingMode.All
    /// // Using trackable collection wrapper due to [TrackCollection] attribute
    /// private TrackableList&lt;string&gt; _items;
    /// public partial List&lt;string&gt; Items
    /// {
    ///     get => _items;
    ///     set { _changeTracker?.RecordChange(nameof(Items), _items, value); _items = value != null ? new TrackableList&lt;string&gt;(value) : null; }
    /// }
    /// </code>
    /// </remarks>
    private static void GenerateProperty(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        // Add comment explaining why this property is tracked
        if (property.IsTrackOnly)
            sourceBuilder.AppendLine($"{indent}// This property is tracked because it has the [TrackOnly] attribute");
        else
            sourceBuilder.AppendLine($"{indent}// This property is tracked by default based on TrackingMode.All");

        if (property.HasTrackCollection)
            sourceBuilder.AppendLine(
                $"{indent}// Using trackable collection wrapper due to [TrackCollection] attribute");

        GenerateBackingField(sourceBuilder, property, indent);
        GeneratePropertyDeclaration(sourceBuilder, property, indent);
        GeneratePropertyAccessors(sourceBuilder, property, indent);

        sourceBuilder.AppendLine($"{indent}}}")
            .AppendLine();
    }

    /// <summary>
    /// Generate the backing field for a property
    /// </summary>
    /// <remarks>
    /// Example output for a standard property:
    /// <code>
    /// private string _name;
    /// </code>
    /// 
    /// Example output for a trackable collection:
    /// <code>
    /// private TrackableList&lt;string&gt; _items;
    /// </code>
    /// </remarks>
    private static void GenerateBackingField(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var nullableAnnotation = property.IsNullable ? "?" : "";
        var staticModifier = property.IsStatic ? $"{Constants.Keywords.StaticKeyword} " : "";
        var fieldType = property.CollectionWrapperType is not null
            ? $"{property.CollectionWrapperType}{nullableAnnotation}"
            : property.TypeName;

        sourceBuilder.AppendLine($"{indent}private {staticModifier}{fieldType} {property.BackingFieldName};");
    }

    /// <summary>
    /// Generate the property declaration with appropriate modifiers
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>
    /// public partial string Name
    /// {
    /// </code>
    /// </remarks>
    private static void GeneratePropertyDeclaration(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        // Build property modifiers
        var modifiers = BuildPropertyModifiers(property);

        // Property declaration
        sourceBuilder.AppendLine($"{indent}{string.Join(" ", modifiers)} {property.TypeName} {property.Name}")
            .AppendLine($"{indent}{{");
    }

    /// <summary>
    /// Build a list of property modifiers based on property characteristics
    /// </summary>
    private static List<string> BuildPropertyModifiers(PropertyInfo property)
    {
        var modifiers = new List<string>(7) // Pre-size for common case
        {
            SymbolHelper.GetAccessibilityAsString(property.PropertyAccessibility)
        };

        if (property.IsStatic) modifiers.Add(Constants.Keywords.StaticKeyword);
        if (property.IsVirtual) modifiers.Add("virtual");
        if (property.IsOverride) modifiers.Add("override");
        if (property.IsSealed) modifiers.Add("sealed");
        if (property.IsAbstract) modifiers.Add("abstract");
        modifiers.Add(Constants.Keywords.PartialKeyword);

        return modifiers;
    }

    /// <summary>
    /// Generate property accessors (get, set/init)
    /// </summary>
    private static void GeneratePropertyAccessors(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var accessorIndent = $"{indent}    ";

        // Generate getter if present
        if (property.HasGetter)
        {
            GenerateGetter(sourceBuilder, property, accessorIndent);
        }

        // Generate setter if present
        if (property.HasSetter) GenerateSetter(sourceBuilder, property, accessorIndent);
    }

    /// <summary>
    /// Generate the getter for a property
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>get => _name;</code>
    /// Or for abstract properties:
    /// <code>get;</code>
    /// </remarks>
    private static void GenerateGetter(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var getterAccessibility =
            GetAccessorAccessibility(property.GetterAccessibility, property.PropertyAccessibility);

        if (!property.IsAbstract)
        {
            sourceBuilder.AppendLine(
                $"{indent}{getterAccessibility}{Constants.Keywords.GetAccessor} => {property.BackingFieldName};");
        }
        else
        {
            sourceBuilder.AppendLine($"{indent}{getterAccessibility}{Constants.Keywords.GetAccessor};");
        }
    }

    /// <summary>
    /// Generate the setter for a property
    /// </summary>
    /// <remarks>
    /// Example output for a standard property:
    /// <code>
    /// set { _changeTracker?.RecordChange(nameof(Name), _name, value); _name = value; }
    /// </code>
    /// 
    /// Example output for a collection property:
    /// <code>
    /// set { _changeTracker?.RecordChange(nameof(Items), _items, value); _items = value != null ? new TrackableListstring>(value) : null; }
    /// </code>
    /// 
    /// Example output for an abstract property:
    /// <code>set;</code>
    /// </remarks>
    private static void GenerateSetter(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var setterAccessibility =
            GetAccessorAccessibility(property.SetterAccessibility, property.PropertyAccessibility);
        var accessorKeyword =
            property.IsSetterInitOnly ? Constants.Keywords.InitAccessor : Constants.Keywords.SetAccessor;

        if (!property.IsAbstract)
        {
            sourceBuilder.Append($"{indent}{setterAccessibility}{accessorKeyword} {{ ");
            GenerateSetterBody(sourceBuilder, property);
            sourceBuilder.AppendLine(" }");
        }
        else
        {
            sourceBuilder.AppendLine($"{indent}{setterAccessibility}{accessorKeyword};");
        }
    }

    /// <summary>
    /// Get the accessibility modifier for an accessor, if different from the property
    /// </summary>
    private static string GetAccessorAccessibility(Accessibility accessorAccessibility,
        Accessibility propertyAccessibility)
    {
        return accessorAccessibility != propertyAccessibility
            ? $"{SymbolHelper.GetAccessibilityAsString(accessorAccessibility)} "
            : "";
    }

    /// <summary>
    /// Generate the body of a setter, handling tracking and special collection handling
    /// </summary>
    /// <remarks>
    /// Example output for a standard property:
    /// <code>
    /// _changeTracker?.RecordChange(nameof(Name), _name, value); _name = value;
    /// </code>
    /// 
    /// Example output for a collection property:
    /// <code>
    /// _changeTracker?.RecordChange(nameof(Items), _items, value); _items = value != null ? new TrackableList&ltstring&lg(value) : null;
    /// </code>
    /// </remarks>
    private static void GenerateSetterBody(StringBuilder sourceBuilder, PropertyInfo property)
    {
        // Only add change tracking for non-static properties
        if (!property.IsStatic)
        {
            sourceBuilder
                .Append($"_changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value); ")
                .Append(GeneratePropertyAssignment(property));
        }
        else
        {
            // For static properties, simply set the value without tracking
            sourceBuilder.Append(GeneratePropertyAssignment(property));
        }
    }

    /// <summary>
    /// Generate the assignment code for a property based on its type
    /// </summary>
    /// <remarks>
    /// Example output for a standard property:
    /// <code>
    /// _name = value;
    /// </code>
    /// 
    /// Example output for a collection property:
    /// <code>
    /// _items = value != null ? new TrackableList&lt;string&gt;(value) : null;
    /// </code>
    /// </remarks>
    private static string GeneratePropertyAssignment(PropertyInfo property)
    {
        if (property.CollectionWrapperType is not null)
        {
            return
                $"{property.BackingFieldName} = value != null ? new {property.CollectionWrapperType}(value) : null;";
        }
        else
        {
            return $"{property.BackingFieldName} = value;";
        }
    }

    #endregion
}