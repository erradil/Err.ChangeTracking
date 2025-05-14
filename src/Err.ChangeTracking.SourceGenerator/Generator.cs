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
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Set up the pipeline for the Trackable attribute
        var trackableTypes =
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Constants.TrackableAttributeFullName,
                    static (node, _) =>
                        node is TypeDeclarationSyntax typeDecl && HasPartialModifier(typeDecl),
                    static (ctx, _) => GetTypeInfo(ctx))
                .Where(static t => t.TypeSymbol is not null);

        // Transform the type declaration and symbol into our strongly-typed metadata
        var
            typeMetadata = trackableTypes.Select(static (typeData, _) => ExtractMetadata(typeData.TypeSymbol));

        // Register the source output
        context.RegisterSourceOutput(typeMetadata,
            static (ctx, data) => GenerateSource(ctx, data.TypeInfo, data.Properties, data.AlreadyImplementsTrackable));
    }

    /// <summary>
    ///     Check if the node has a partial modifier
    /// </summary>
    private static bool HasPartialModifier(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    ///     Transform the GeneratorAttributeSyntaxContext to get the type declaration and symbol
    /// </summary>
    private static (TypeDeclarationSyntax TypeDeclaration, INamedTypeSymbol TypeSymbol) GetTypeInfo(
        GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is TypeDeclarationSyntax typeDecl &&
            ctx.TargetSymbol is INamedTypeSymbol typeSymbol)
            return (typeDecl, typeSymbol);

        return (null, null);
    }

    /// <summary>
    ///     Extract all the metadata needed for code generation in a single pass
    /// </summary>
    private static (TypeInfo TypeInfo, ImmutableArray<PropertyInfo> Properties, bool AlreadyImplementsTrackable)
        ExtractMetadata(
            INamedTypeSymbol typeSymbol)
    {
        // Create TypeInfo
        var typeInfo = new TypeInfo(
            typeSymbol.Name,
            typeSymbol.ContainingNamespace.IsGlobalNamespace ? null : typeSymbol.ContainingNamespace.ToDisplayString(),
            SymbolHelper.DetermineTypeKind(typeSymbol),
            typeSymbol.DeclaredAccessibility,
            SymbolHelper.GetTypeModifiers(typeSymbol),
            SymbolHelper.ExtractContainingTypeInfos(typeSymbol)
        );

        // Check if type already implements the ITrackable interface
        var alreadyImplementsTrackable = SymbolHelper.ImplementsTrackableInterface(typeSymbol);

        // Get tracking mode from type
        var trackingMode = SymbolHelper.GetTrackingMode(typeSymbol);

        // Extract properties that should be tracked
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();

        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            // Skip non-partial properties
            if (!member.IsPartial())
                continue;

            // Check tracking attributes
            var isTrackOnly = SymbolHelper.HasAttribute(member, Constants.TrackOnlyAttributeFullName);
            var isNotTracked = SymbolHelper.HasAttribute(member, Constants.NotTrackedAttributeFullName);
            var isTrackableCollection = SymbolHelper.IsTrackableCollection(member);

            // Determine if this property should be tracked
            var isTrackable = !isNotTracked && (
                isTrackableCollection ||
                trackingMode != TrackingMode.OnlyMarked ||
                isTrackOnly
            );

            // Skip if not trackable
            if (!isTrackable)
                continue;

            properties.Add(ExtractPropertyInfo(member));
        }

        return (typeInfo, properties.ToImmutable(), alreadyImplementsTrackable);
    }

    /// <summary>
    ///     Extract property information from a property symbol
    /// </summary>
    private static PropertyInfo ExtractPropertyInfo(IPropertySymbol propertySymbol)
    {
        // Check if this is a trackable collection
        var isCollection = false;
        string trackableCollectionType = null;

        if (SymbolHelper.HasAttribute(propertySymbol, Constants.TrackCollectionAttributeFullName))
        {
            var propertyType = propertySymbol.Type;

            if (propertyType is INamedTypeSymbol namedType)
            {
                var originalDef = namedType.OriginalDefinition.ToDisplayString();

                if (originalDef == "System.Collections.Generic.List<T>")
                {
                    isCollection = true;
                    var elementType = namedType.TypeArguments[0].ToDisplayString();
                    trackableCollectionType = $"{Constants.TrackableListFullName}<{elementType}>";
                }
                else if (originalDef == "System.Collections.Generic.Dictionary<TKey, TValue>")
                {
                    isCollection = true;
                    var keyType = namedType.TypeArguments[0].ToDisplayString();
                    var valueType = namedType.TypeArguments[1].ToDisplayString();
                    trackableCollectionType = $"{Constants.TrackableDictionaryFullName}<{keyType}, {valueType}>";
                }
            }
        }

        return new PropertyInfo(
            propertySymbol.Name,
            propertySymbol.Type.ToDisplayString(),
            $"_{char.ToLowerInvariant(propertySymbol.Name[0])}{propertySymbol.Name.Substring(1)}",
            propertySymbol.IsStatic,
            propertySymbol.IsVirtual,
            propertySymbol.IsOverride,
            propertySymbol.IsAbstract,
            propertySymbol.IsSealed,
            propertySymbol.DeclaredAccessibility,
            propertySymbol.GetMethod != null,
            propertySymbol.SetMethod != null,
            propertySymbol.SetMethod?.IsInitOnly ?? false,
            propertySymbol.GetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable,
            propertySymbol.SetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable,
            isCollection,
            trackableCollectionType,
            propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated
        );
    }

    /// <summary>
    ///     Generate source code for a type
    /// </summary>
    private static void GenerateSource(
        SourceProductionContext context,
        TypeInfo typeInfo,
        ImmutableArray<PropertyInfo> properties,
        bool alreadyImplementsTrackable)
    {
        if (properties.IsEmpty)
            return;

        var sourceBuilder = new StringBuilder();

        // Add header and nullable enable
        sourceBuilder.AppendLine("// <auto-generated>");
        sourceBuilder.AppendLine("// This code was generated by the Err.ChangeTracking Source Generator");
        sourceBuilder.AppendLine(
            "// Changes to this file may cause incorrect behavior and will be lost if the code is regenerated");
        sourceBuilder.AppendLine("// </auto-generated>");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("#nullable enable");
        sourceBuilder.AppendLine();

        // Add namespace if needed
        if (typeInfo.Namespace != null)
        {
            sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};");
            sourceBuilder.AppendLine();
        }

        // Generate the type based on whether it's nested or not
        if (typeInfo.ContainingTypeInfos.Count > 0)
            GenerateNestedType(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);
        else
            GenerateStandardType(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);

        var fileName = $"{typeInfo.GetFullName()}.g.cs";
        context.AddSource(fileName, SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    ///     Generate a standard (non-nested) type
    /// </summary>
    private static void GenerateStandardType(
        StringBuilder sourceBuilder,
        TypeInfo typeInfo,
        ImmutableArray<PropertyInfo> properties,
        bool alreadyImplementsTrackable)
    {
        // Build type declaration with all modifiers
        var accessibility = SymbolHelper.GetAccessibilityAsString(typeInfo.Accessibility);
        var modifiers = string.Join(" ", typeInfo.Modifiers);
        var modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";

        // Add the ITrackable<T> interface if not already implemented
        var qualifiedTypeName = typeInfo.Name;
        var interfaceImplementation = !alreadyImplementsTrackable
            ? $" : {Constants.ITrackableFullName}<{qualifiedTypeName}>"
            : "";

        sourceBuilder.AppendLine(
            $"{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
        sourceBuilder.AppendLine("{");

        // If we're adding the interface implementation, generate the required members
        if (!alreadyImplementsTrackable)
        {
            sourceBuilder.AppendLine(
                $"    private {Constants.IChangeTrackingFullName}<{qualifiedTypeName}>? _changeTracker;");
            sourceBuilder.AppendLine(
                $"    public {Constants.IChangeTrackingFullName}<{qualifiedTypeName}> GetChangeTracker() => _changeTracker ??= new {Constants.ChangeTrackingFullName}<{qualifiedTypeName}>(this);");
            sourceBuilder.AppendLine();
        }

        // Generate each property
        foreach (var property in properties)
            GenerateProperty(sourceBuilder, property, "    ");

        sourceBuilder.AppendLine("}");
    }

    /// <summary>
    ///     Generate nested type hierarchy for embedded types
    /// </summary>
    private static void GenerateNestedType(
        StringBuilder sourceBuilder,
        TypeInfo typeInfo,
        ImmutableArray<PropertyInfo> properties,
        bool alreadyImplementsTrackable)
    {
        // Get fully qualified type name for interface implementation
        var fullTypeName = typeInfo.GetFullName();
        var typeName = fullTypeName.Substring(fullTypeName.LastIndexOf('.') + 1);

        // Indent management
        var indent = 0;

        // For each containing type, open a partial type declaration
        foreach (var containingType in typeInfo.ContainingTypeInfos)
        {
            var indentStr = new string(' ', indent * 4);

            var containingAccessibility = SymbolHelper.GetAccessibilityAsString(containingType.Accessibility);
            var containingModifiers = string.Join(" ", containingType.Modifiers);
            var containingModifiersWithSpace =
                !string.IsNullOrEmpty(containingModifiers) ? containingModifiers + " " : "";

            sourceBuilder.AppendLine(
                $"{indentStr}{containingAccessibility} {containingModifiersWithSpace}partial {containingType.Kind} {containingType.Name}");
            sourceBuilder.AppendLine($"{indentStr}{{");

            indent++;
        }

        // Now generate the actual type we're tracking
        var typeIndent = new string(' ', indent * 4);

        // Build type declaration with all modifiers
        var accessibility = SymbolHelper.GetAccessibilityAsString(typeInfo.Accessibility);
        var modifiers = string.Join(" ", typeInfo.Modifiers);
        var modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";

        // Add the ITrackable<T> interface if not already implemented
        var interfaceImplementation = !alreadyImplementsTrackable
            ? $" : {Constants.ITrackableFullName}<{fullTypeName}>"
            : "";

        sourceBuilder.AppendLine(
            $"{typeIndent}{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
        sourceBuilder.AppendLine($"{typeIndent}{{");
        indent++;

        // Add trackable implementation
        if (!alreadyImplementsTrackable)
        {
            var memberIndent = new string(' ', indent * 4);
            sourceBuilder.AppendLine(
                $"{memberIndent}private {Constants.IChangeTrackingFullName}<{fullTypeName}>? _changeTracker;");
            sourceBuilder.AppendLine(
                $"{memberIndent}public {Constants.IChangeTrackingFullName}<{fullTypeName}> GetChangeTracker() => _changeTracker ??= new {Constants.ChangeTrackingFullName}<{fullTypeName}>(this);");
            sourceBuilder.AppendLine();
        }

        // Generate each property
        foreach (var property in properties)
            GenerateProperty(sourceBuilder, property, new string(' ', indent * 4));

        // Close all type declarations
        for (var i = indent; i > 0; i--) sourceBuilder.AppendLine(new string(' ', (i - 1) * 4) + "}");
    }

    /// <summary>
    ///     Generate a property implementation with custom indentation
    /// </summary>
    private static void GenerateProperty(StringBuilder sourceBuilder, PropertyInfo property, string indent)
    {
        var nullableAnnotation = property.IsNullable ? "?" : "";

        // Generate backing field with appropriate type
        var staticModifier = property.IsStatic ? "static " : "";
        var fieldType = property.IsCollection
            ? $"{property.TrackableCollectionType}{nullableAnnotation}"
            : property.TypeName;

        sourceBuilder.AppendLine($"{indent}private {staticModifier}{fieldType} {property.BackingFieldName};");

        // Build property modifiers
        var modifiers = new List<string>
        {
            SymbolHelper.GetAccessibilityAsString(property.PropertyAccessibility)
        };

        if (property.IsStatic) modifiers.Add("static");
        if (property.IsVirtual) modifiers.Add("virtual");
        if (property.IsOverride) modifiers.Add("override");
        if (property.IsSealed) modifiers.Add("sealed");
        if (property.IsAbstract) modifiers.Add("abstract");
        modifiers.Add("partial");

        // Property declaration
        sourceBuilder.AppendLine($"{indent}{string.Join(" ", modifiers)} {property.TypeName} {property.Name}");
        sourceBuilder.AppendLine($"{indent}{{");

        // Generate getter if present
        if (property.HasGetter)
        {
            var getterAccessibility = property.GetterAccessibility != property.PropertyAccessibility
                ? $"{SymbolHelper.GetAccessibilityAsString(property.GetterAccessibility)} "
                : "";

            if (!property.IsAbstract)
                sourceBuilder.AppendLine($"{indent}    {getterAccessibility}get => {property.BackingFieldName};");
            else
                sourceBuilder.AppendLine($"{indent}    {getterAccessibility}get;");
        }

        // Generate setter if present
        if (property.HasSetter)
        {
            var setterAccessibility = property.SetterAccessibility != property.PropertyAccessibility
                ? $"{SymbolHelper.GetAccessibilityAsString(property.SetterAccessibility)} "
                : "";

            var accessorKeyword = property.IsSetterInitOnly ? "init" : "set";

            if (!property.IsAbstract)
            {
                sourceBuilder.Append($"{indent}    {setterAccessibility}{accessorKeyword} {{ ");

                // Only add change tracking for non-static properties
                if (!property.IsStatic)
                {
                    sourceBuilder.Append(
                        $"_changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value); ");

                    // Special handling for collection properties
                    if (property.IsCollection)
                        sourceBuilder.Append($"{property.BackingFieldName} = value != null ? " +
                                             $"new {property.TrackableCollectionType}(value) : null;");
                    else
                        sourceBuilder.Append($"{property.BackingFieldName} = value;");
                }
                else
                {
                    // For static properties, simply set the value without tracking
                    if (property.IsCollection)
                        sourceBuilder.Append($"{property.BackingFieldName} = value != null ? " +
                                             $"new {property.TrackableCollectionType}(value) : null;");
                    else
                        sourceBuilder.Append($"{property.BackingFieldName} = value;");
                }

                sourceBuilder.AppendLine(" }");
            }
            else
            {
                sourceBuilder.AppendLine($"{indent}    {setterAccessibility}{accessorKeyword};");
            }
        }

        sourceBuilder.AppendLine($"{indent}}}");
        sourceBuilder.AppendLine();
    }
}