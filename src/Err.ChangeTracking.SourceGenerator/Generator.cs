using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Err.ChangeTracking.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Err.ChangeTracking.SourceGenerator;

[Generator]
public class OptimizedChangeTrackingGenerator : IIncrementalGenerator
{
    // String constants
    private const string PartialKeyword = "partial";
    private const string StaticKeyword = "static";
    private const string GetAccessor = "get";
    private const string SetAccessor = "set";
    private const string InitAccessor = "init";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Set up the pipeline for the Trackable attribute
        IncrementalValuesProvider<(TypeDeclarationSyntax TypeDeclaration, INamedTypeSymbol TypeSymbol)> trackableTypes =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Constants.TrackableAttributeFullName,
                    static (node, _) =>
                        node is TypeDeclarationSyntax typeDecl && HasPartialModifier(typeDecl),
                    static (ctx, _) => GetTypeInfo(ctx))
                .Where(static t => t.TypeSymbol is not null);

        // Transform the type declaration and symbol into our strongly-typed metadata
        var typeMetadata =
            trackableTypes
                .Select(static (typeData, _) => ExtractMetadata(typeData.TypeSymbol));

        // Register the source output
        context.RegisterSourceOutput(typeMetadata,
            static (ctx, data) => GenerateSource(ctx, data.TypeInfo, data.Properties));
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
    private static (TypeDeclarationSyntax TypeDeclaration, INamedTypeSymbol TypeSymbol) GetTypeInfo(
        GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is TypeDeclarationSyntax typeDecl &&
            ctx.TargetSymbol is INamedTypeSymbol typeSymbol)
        {
            return (typeDecl, typeSymbol);
        }

        return (null, null);
    }

    /// <summary>
    /// Extract all the metadata needed for code generation in a single pass
    /// </summary>
    private static (TypeInfo TypeInfo, ImmutableArray<PropertyInfo> Properties)
        ExtractMetadata(
            INamedTypeSymbol typeSymbol)
    {
        // Extract properties that should be tracked
        var properties = GetTrackableProperties(typeSymbol);

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

        return (typeInfo, properties);
    }

    /// <summary>
    /// Get all properties that should be tracked based on attributes and tracking mode
    /// </summary>
    private static ImmutableArray<PropertyInfo> GetTrackableProperties(INamedTypeSymbol typeSymbol)
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
            var isTrackOnly = SymbolHelper.HasAttribute(member, Constants.TrackOnlyAttributeFullName);
            var isNotTracked = SymbolHelper.HasAttribute(member, Constants.NotTrackedAttributeFullName);

            // If explicitly marked not to track, then don't track
            if (isNotTracked)
                continue;

            // If mode is OnlyMarked, then only properties with [TrackOnly] are tracked
            if (trackingMode == TrackingMode.OnlyMarked && !isTrackOnly)
                continue;

            // If the type is already TrackableList or TrackableDictionary, not need to track
            if (member.IsTypeOf(Constants.TrackableDictionaryFullName) ||
                member.IsTypeOf(Constants.TrackableListFullName))
                continue;

            // We've determined this property should be tracked, now check if it's a collection
            var (isCollection, trackableCollectionType) = GetCollectionTypeInfo(member);

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
                IsCollection = isCollection,
                TrackableCollectionType = trackableCollectionType,
                IsNullable = member.Type.NullableAnnotation == NullableAnnotation.Annotated
            };

            properties.Add(trackableProperty);
        }

        return properties.ToImmutable();
    }

    /// <summary>
    /// Determines if a property is a trackable collection and gets the appropriate wrapper type
    /// </summary>
    private static (bool isCollection, string trackableCollectionType) GetCollectionTypeInfo(
        IPropertySymbol propertySymbol)
    {
        // Only check for TrackCollection attribute - this is the ONLY condition for replacing with trackable collections
        if (!SymbolHelper.HasAttribute(propertySymbol, Constants.TrackCollectionAttributeFullName))
            return (false, null);

        if (propertySymbol.Type is not INamedTypeSymbol namedType)
            return (false, null);

        if (propertySymbol.IsTypeOf("System.Collections.Generic.List<T>"))
        {
            var argType = namedType.TypeArguments[0].ToDisplayString();
            return (true, $"{Constants.TrackableListFullName}<{argType}>");
        }

        if (propertySymbol.IsTypeOf("System.Collections.Generic.Dictionary<TKey, TValue>"))
        {
            var keyType = namedType.TypeArguments[0].ToDisplayString();
            var valueType = namedType.TypeArguments[1].ToDisplayString();
            return (true, $"{Constants.TrackableDictionaryFullName}<{keyType}, {valueType}>");
        }

        return (false, null);
    }

    /// <summary>
    /// Generate source code for a type
    /// </summary>
    /// <summary>
    /// Generate source code for a type
    /// </summary>
    private static void GenerateSource(
        SourceProductionContext context,
        TypeInfo typeInfo,
        ImmutableArray<PropertyInfo> properties)
    {
        if (properties.IsEmpty)
            return;

        var sourceBuilder = new StringBuilder();

        GenerateFileHeader(sourceBuilder);

        // Add namespace if needed
        if (typeInfo.Namespace != null)
        {
            sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};");
            sourceBuilder.AppendLine();
        }

        // Generate the type (handles both nested and non-nested cases)
        GenerateType(sourceBuilder, typeInfo, properties);

        var fileName = $"{typeInfo.GetFullName()}.g.cs";
        context.AddSource(fileName, SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    ///     Generate a type with its properties, handling both nested and non-nested types
    /// </summary>
    private static void GenerateType(
        StringBuilder sourceBuilder,
        TypeInfo typeInfo,
        ImmutableArray<PropertyInfo> properties)
    {
        // Get the fully qualified type name for interface implementation
        var fullTypeName = typeInfo.GetFullName();
        var typeName = typeInfo.ContainingTypeInfos.Count > 0
            ? fullTypeName
            : typeInfo.Name;

        // Indent management
        var indent = 0;

        // For nested types, generate the containing type hierarchy first
        if (typeInfo.ContainingTypeInfos.Count > 0)
            // For each containing type, open a partial type declaration
            foreach (var containingType in typeInfo.ContainingTypeInfos)
            {
                var indentStr = new string(' ', indent * 4);

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
                    $"{indentStr}{containingTypeDeclaration.Replace($" : {Constants.ITrackableFullName}<{containingType.Name}>", "")}");
                sourceBuilder.AppendLine($"{indentStr}{{");

                indent++;
            }

        // Now generate the actual type
        var typeIndent = new string(' ', indent * 4);

        // Build type declaration with all modifiers
        var typeDeclaration = BuildTypeDeclaration(typeInfo, typeName);
        sourceBuilder.AppendLine($"{typeIndent}{typeDeclaration}");
        sourceBuilder.AppendLine($"{typeIndent}{{");
        indent++;

        // If we're adding the interface implementation, generate the required members
        if (typeInfo.AlreadyImplementsTrackable is false)
            GenerateTrackingImplementation(sourceBuilder, typeName, new string(' ', indent * 4));

        // Generate each property
        foreach (var property in properties)
            GenerateProperty(sourceBuilder, property, new string(' ', indent * 4));

        // Close all type declarations (the current type + any containing types)
        for (var i = indent; i > 0; i--) sourceBuilder.AppendLine(new string(' ', (i - 1) * 4) + "}");
    }

    /// <summary>
    ///     Generate the file header with auto-generated comments and nullable enable directive
    /// </summary>
    private static void GenerateFileHeader(StringBuilder sourceBuilder)
    {
        sourceBuilder.AppendLine("// <auto-generated>");
        sourceBuilder.AppendLine("// This code was generated by the Err.ChangeTracking Source Generator");
        sourceBuilder.AppendLine(
            "// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated");
        sourceBuilder.AppendLine("// </auto-generated>");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("#nullable enable");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Generate tracking implementation for a type
    /// </summary>
    private static void GenerateTrackingImplementation(StringBuilder sourceBuilder, string typeName, string indent)
    {
        sourceBuilder.AppendLine($"{indent}private {Constants.IChangeTrackingFullName}<{typeName}>? _changeTracker;");
        sourceBuilder.AppendLine(
            $"{indent}public {Constants.IChangeTrackingFullName}<{typeName}> GetChangeTracker() => _changeTracker ??= new {Constants.ChangeTrackingFullName}<{typeName}>(this);");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Build the type declaration with appropriate modifiers and interface implementation
    /// </summary>
    private static string BuildTypeDeclaration(TypeInfo typeInfo, string typeName)
    {
        var accessibility = SymbolHelper.GetAccessibilityAsString(typeInfo.Accessibility);
        var modifiers = string.Join(" ", typeInfo.Modifiers);
        var modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? $"{modifiers} " : "";

        // Add the ITrackable<T> interface if not already implemented
        var interfaceImplementation = typeInfo.AlreadyImplementsTrackable is false
            ? $" : {Constants.ITrackableFullName}<{typeName}>"
            : "";

        return
            $"{accessibility} {modifiersWithSpace}{PartialKeyword} {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}";
    }

    /// <summary>
    /// Generate a property implementation with custom indentation
    /// </summary>
    private static void GenerateProperty(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        GenerateBackingField(sourceBuilder, property, indent);
        GeneratePropertyDeclaration(sourceBuilder, property, indent);
        GeneratePropertyAccessors(sourceBuilder, property, indent);

        sourceBuilder.AppendLine($"{indent}}}");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Generate the backing field for a property
    /// </summary>
    private static void GenerateBackingField(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var nullableAnnotation = property.IsNullable ? "?" : "";
        var staticModifier = property.IsStatic ? $"{StaticKeyword} " : "";
        var fieldType = property.IsCollection
            ? $"{property.TrackableCollectionType}{nullableAnnotation}"
            : property.TypeName;

        sourceBuilder.AppendLine($"{indent}private {staticModifier}{fieldType} {property.BackingFieldName};");
    }

    /// <summary>
    ///     Generate the property declaration with appropriate modifiers
    /// </summary>
    private static void GeneratePropertyDeclaration(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        // Build property modifiers
        var modifiers = BuildPropertyModifiers(property);

        // Property declaration
        sourceBuilder.AppendLine($"{indent}{string.Join(" ", modifiers)} {property.TypeName} {property.Name}");
        sourceBuilder.AppendLine($"{indent}{{");
    }

    /// <summary>
    ///     Build a list of property modifiers based on property characteristics
    /// </summary>
    private static List<string> BuildPropertyModifiers(PropertyInfo property)
    {
        var modifiers = new List<string>
        {
            SymbolHelper.GetAccessibilityAsString(property.PropertyAccessibility)
        };

        if (property.IsStatic) modifiers.Add(StaticKeyword);
        if (property.IsVirtual) modifiers.Add("virtual");
        if (property.IsOverride) modifiers.Add("override");
        if (property.IsSealed) modifiers.Add("sealed");
        if (property.IsAbstract) modifiers.Add("abstract");
        modifiers.Add(PartialKeyword);

        return modifiers;
    }

    /// <summary>
    ///     Generate property accessors (get, set/init)
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
    ///     Generate the getter for a property
    /// </summary>
    private static void GenerateGetter(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var getterAccessibility =
            GetAccessorAccessibility(property.GetterAccessibility, property.PropertyAccessibility);

        if (!property.IsAbstract)
            sourceBuilder.AppendLine($"{indent}{getterAccessibility}{GetAccessor} => {property.BackingFieldName};");
        else
            sourceBuilder.AppendLine($"{indent}{getterAccessibility}{GetAccessor};");
    }

    /// <summary>
    ///     Generate the setter for a property
    /// </summary>
    private static void GenerateSetter(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var setterAccessibility =
            GetAccessorAccessibility(property.SetterAccessibility, property.PropertyAccessibility);
        var accessorKeyword = property.IsSetterInitOnly ? InitAccessor : SetAccessor;

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
    ///     Get the accessibility modifier for an accessor, if different from the property
    /// </summary>
    private static string GetAccessorAccessibility(Accessibility accessorAccessibility,
        Accessibility propertyAccessibility)
    {
        return accessorAccessibility != propertyAccessibility
            ? $"{SymbolHelper.GetAccessibilityAsString(accessorAccessibility)} "
            : "";
    }

    /// <summary>
    ///     Generate the body of a setter, handling tracking and special collection handling
    /// </summary>
    private static void GenerateSetterBody(StringBuilder sourceBuilder, PropertyInfo property)
    {
        // Only add change tracking for non-static properties
        if (!property.IsStatic)
        {
            sourceBuilder.Append(
                $"_changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value); ");

            // Assignment based on collection type
            sourceBuilder.Append(GeneratePropertyAssignment(property));
        }
        else
        {
            // For static properties, simply set the value without tracking
            sourceBuilder.Append(GeneratePropertyAssignment(property));
        }
    }

    /// <summary>
    ///     Generate the assignment code for a property based on its type
    /// </summary>
    private static string GeneratePropertyAssignment(PropertyInfo property)
    {
        if (property.IsCollection)
            return
                $"{property.BackingFieldName} = value != null ? new {property.TrackableCollectionType}(value) : null;";
        else
            return $"{property.BackingFieldName} = value;";
    }
}