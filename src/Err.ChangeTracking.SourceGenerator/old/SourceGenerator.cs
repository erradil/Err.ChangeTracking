/*using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Err.ChangeTracking.SourceGenerator;

//[Generator]
public class PartialPropertyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Filter for trackable type declarations with partial properties
        IncrementalValuesProvider<TypeDeclarationSyntax> trackableTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => IsPotentialTrackableType(s),
                static (ctx, _) => GetTrackableType(ctx))
            .Where(static m => m is not null)!;

        // Combine the compilation with our filtered type declarations
        IncrementalValueProvider<(Compilation, ImmutableArray<TypeDeclarationSyntax>)> compilationAndTypes =
            context.CompilationProvider.Combine(trackableTypes.Collect());

        // Register the source output action
        context.RegisterSourceOutput(compilationAndTypes,
            static (spc, source) => GenerateTrackableImplementations(source.Item1, source.Item2, spc));
    }


    #region Type and Property Detection

    /// <summary>
    ///     Initial syntactic filter for potential trackable types
    /// </summary>
    private static bool IsPotentialTrackableType(SyntaxNode node)
    {
        // Must be a class, struct, or record declaration
        if (node is not TypeDeclarationSyntax typeDecl) return false;

        // Must have the 'partial' modifier
        var isPartial = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
        if (!isPartial) return false;

        // Must have at least one attribute list
        if (typeDecl.AttributeLists.Count == 0) return false;

        // Must have at least one partial property
        var hasPartialProperty = typeDecl.Members.OfType<PropertyDeclarationSyntax>()
            .Any(p => p.Modifiers.Any(SyntaxKind.PartialKeyword));

        return hasPartialProperty;
    }

    /// <summary>
    ///     Semantic filter to confirm a type has the [Trackable] attribute
    /// </summary>
    private static TypeDeclarationSyntax? GetTrackableType(GeneratorSyntaxContext context)
    {
        var typeDeclarationSyntax = (TypeDeclarationSyntax)context.Node;

        return HasTrackableAttribute(context, typeDeclarationSyntax)
            ? typeDeclarationSyntax
            : null;
    }

    /// <summary>
    ///     Check if the type declaration has the [Trackable] attribute
    /// </summary>
    private static bool HasTrackableAttribute(GeneratorSyntaxContext context,
        TypeDeclarationSyntax typeDeclarationSyntax)
    {
        foreach (var attributeListSyntax in typeDeclarationSyntax.AttributeLists)
        foreach (var attributeSyntax in attributeListSyntax.Attributes)
        {
            if (context.SemanticModel.GetSymbolInfo(attributeSyntax)
                    .Symbol is not IMethodSymbol attributeSymbol)
                continue;

            var attributeFullName = attributeSymbol.ContainingType.ToDisplayString();
            if (attributeFullName == Constants.TrackableAttributeFullName) return true;
        }

        return false;
    }

    /// <summary>
    ///     Find all trackable partial properties in a type declaration based on tracking rules
    /// </summary>
    private static IEnumerable<(PropertyDeclarationSyntax Syntax, IPropertySymbol Symbol)> GetTrackableProperties(
        TypeDeclarationSyntax typeSyntax, SemanticModel semanticModel)
    {
        // Get tracking mode from type
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax);
        var trackingMode = GetTrackingMode(typeSymbol);

        foreach (var member in typeSyntax.Members)
        {
            // Only process partial properties
            if (member is not PropertyDeclarationSyntax propertySyntax ||
                !propertySyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                continue;

            var propertySymbol = semanticModel.GetDeclaredSymbol(propertySyntax);
            if (propertySymbol == null) continue;

            // Check tracking attributes
            var isTrackOnly = SymbolHelper.HasAttribute(propertySymbol, Constants.TrackOnlyAttributeFullName);
            var isNotTracked = SymbolHelper.HasAttribute(propertySymbol, Constants.NotTrackedAttributeFullName);

            // Check if already a trackable collection
            var isTrackableCollection = SymbolHelper.IsTrackableCollection(propertySymbol);

            // Determine if this property should be tracked
            var isTrackable = !isNotTracked ||
                              isTrackableCollection ||
                              trackingMode != TrackingMode.OnlyMarked || isTrackOnly;

            // Skip if not trackable
            if (!isTrackable) continue;

            yield return (propertySyntax, propertySymbol);
        }
    }


    /// <summary>
    ///     Get tracking mode from the [Trackable] attribute
    /// </summary>
    private static TrackingMode GetTrackingMode(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol == null) return TrackingMode.All; // Default

        foreach (var attribute in typeSymbol.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == Constants.TrackableAttributeFullName)
            {
                // Check if attribute has constructor arguments for tracking mode
                if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Mode").Value.Value is int
                    trackingModeValue)
                    return (TrackingMode)trackingModeValue;

                break;
            }

        return TrackingMode.All; // Default if isn't specified
    }

    #endregion

    #region Property Information Extraction

    /// <summary>
    ///     Extract property information from a property symbol
    /// </summary>
    private static PropertyInfo ExtractPropertyInfo(IPropertySymbol propertySymbol)
    {
        return new PropertyInfo(propertySymbol);
    }

    #endregion

    #region Source Generation

    /// <summary>
    ///     Main source generation method
    /// </summary>
    private static void GenerateTrackableImplementations(
        Compilation compilation,
        ImmutableArray<TypeDeclarationSyntax> types,
        SourceProductionContext context)
    {
        if (types.IsDefaultOrEmpty) return;

        foreach (var typeSyntax in types.Distinct())
        {
            var semanticModel = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax);

            if (typeSymbol == null) continue;

            var typeInfo = ExtractTypeInfo(typeSymbol);
            var properties = GetTrackableProperties(typeSyntax, semanticModel)
                .Select(p => ExtractPropertyInfo(p.Symbol))
                .ToList();

            if (properties.Count > 0)
            {
                var generatedSource = GeneratePartialTypeWithProperties(typeInfo, properties,
                    SymbolHelper.ImplementsTrackableInterface(typeSymbol));
                var fileName = $"{typeInfo.GetFullName()}.g.cs";
                context.AddSource(fileName, SourceText.From(generatedSource, Encoding.UTF8));
            }
        }
    }

    /// <summary>
    ///     Extract type information from a type symbol
    /// </summary>
    private static TypeInfo ExtractTypeInfo(INamedTypeSymbol typeSymbol)
    {
        return new TypeInfo(typeSymbol);
    }

    /// <summary>
    ///     Gets the containing type names in the format "OuterType.MiddleType"
    /// </summary>
    private static string GetContainingTypeNames(TypeInfo typeInfo)
    {
        if (typeInfo.ContainingTypeInfos.Count == 0)
            return string.Empty;

        return string.Join(".", typeInfo.ContainingTypeInfos.Select(t => t.Name));
    }

    /// <summary>
    ///     Generate the entire partial type implementation with its properties
    /// </summary>
    private static string GeneratePartialTypeWithProperties(TypeInfo typeInfo, List<PropertyInfo> properties,
        bool alreadyImplementsTrackable)
    {
        var sourceBuilder = new StringBuilder();

        // Add namespace if needed
        if (typeInfo.Namespace != null)
        {
            sourceBuilder.AppendLine("#nullable enable");
            sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};");
            sourceBuilder.AppendLine();
        }

        // For nested types, we need to generate a nested hierarchy
        if (typeInfo.ContainingTypeInfos.Count > 0)
            GenerateNestedTypeHierarchy(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);
        else
            // For non-nested types, generate the standard way
            GenerateStandardType(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);

        return sourceBuilder.ToString();
    }

    /// <summary>
    ///     Generate a standard (non-nested) type
    /// </summary>
    private static void GenerateStandardType(StringBuilder sourceBuilder, TypeInfo typeInfo,
        List<PropertyInfo> properties, bool alreadyImplementsTrackable)
    {
        sourceBuilder.AppendLine($"// Auto-generated for {typeInfo.Name} due to [TrackableAttribute]");

        // Build type declaration with all modifiers
        var accessibility = GetAccessibilityAsString(typeInfo.Accessibility);
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
        if (!alreadyImplementsTrackable) GenerateTrackableImplementation(sourceBuilder, qualifiedTypeName);

        // Generate each property
        foreach (var property in properties) GeneratePropertyImplementation(sourceBuilder, property);

        sourceBuilder.AppendLine("}");
    }

    /// <summary>
    ///     Generate nested type hierarchy for embedded types
    /// </summary>
    private static void GenerateNestedTypeHierarchy(StringBuilder sourceBuilder, TypeInfo typeInfo,
        List<PropertyInfo> properties, bool alreadyImplementsTrackable)
    {
        // Get fully qualified type name for interface implementation
        var containingTypeNames = GetContainingTypeNames(typeInfo);
        var qualifiedTypeName = $"{containingTypeNames}.{typeInfo.Name}";

        // Generate the containing type declarations (outer to inner)
        var indent = 0;

        // For each containing type, open a partial type declaration
        for (var i = 0; i < typeInfo.ContainingTypeInfos.Count; i++)
        {
            var containingType = typeInfo.ContainingTypeInfos[i];
            var indentStr = new string(' ', indent * 4);

            var containingAccessibility = GetAccessibilityAsString(containingType.Accessibility);
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
        var accessibility = GetAccessibilityAsString(typeInfo.Accessibility);
        var modifiers = string.Join(" ", typeInfo.Modifiers);
        var modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";

        // Add the ITrackable<T> interface if not already implemented
        var interfaceImplementation = !alreadyImplementsTrackable
            ? $" : {Constants.ITrackableFullName}<{qualifiedTypeName}>"
            : "";

        sourceBuilder.AppendLine($"{typeIndent}// Auto-generated for {typeInfo.Name} due to [TrackableAttribute]");
        sourceBuilder.AppendLine(
            $"{typeIndent}{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
        sourceBuilder.AppendLine($"{typeIndent}{{");

        indent++;

        // If we're adding the interface implementation, generate the required members with proper indentation
        if (!alreadyImplementsTrackable)
        {
            var memberIndent = new string(' ', indent * 4);

            sourceBuilder.AppendLine(
                $"{memberIndent}private {Constants.IChangeTrackingFullName}<{qualifiedTypeName}>? _changeTracker;");
            sourceBuilder.AppendLine(
                $"{memberIndent}public {Constants.IChangeTrackingFullName}<{qualifiedTypeName}> GetChangeTracker() => _changeTracker ??= new {Constants.ChangeTrackingFullName}<{qualifiedTypeName}>(this);");
            sourceBuilder.AppendLine();
        }

        // Generate each property with proper indentation
        foreach (var property in properties) GeneratePropertyImplementationWithIndent(sourceBuilder, property, indent);

        // Close type and all containing types
        for (var i = indent; i > 0; i--)
        {
            var closeIndent = new string(' ', (i - 1) * 4);
            sourceBuilder.AppendLine($"{closeIndent}}}");
        }
    }

    /// <summary>
    ///     Generate a property implementation with custom indentation
    /// </summary>
    private static void GeneratePropertyImplementationWithIndent(StringBuilder sourceBuilder, PropertyInfo property,
        int indentLevel)
    {
        var indent = new string(' ', indentLevel * 4);
        var fieldIndent = new string(' ', (indentLevel + 1) * 4);
        var codeIndent = new string(' ', (indentLevel + 2) * 4);

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
            GetAccessibilityAsString(property.PropertyAccessibility)
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
                ? $"{GetAccessibilityAsString(property.GetterAccessibility)} "
                : "";

            if (!property.IsAbstract)
                sourceBuilder.AppendLine($"{fieldIndent}{getterAccessibility}get => {property.BackingFieldName};");
            else
                sourceBuilder.AppendLine($"{fieldIndent}{getterAccessibility}get;");
        }

        // Generate setter if present
        if (property.HasSetter)
        {
            var setterAccessibility = property.SetterAccessibility != property.PropertyAccessibility
                ? $"{GetAccessibilityAsString(property.SetterAccessibility)} "
                : "";

            var accessorKeyword = property.IsSetterInitOnly ? "init" : "set";

            if (!property.IsAbstract)
            {
                sourceBuilder.Append($"{fieldIndent}{setterAccessibility}{accessorKeyword}");
                sourceBuilder.Append(" {");

                // Only add change tracking for non-static properties
                if (!property.IsStatic)
                {
                    // Special handling for collection properties
                    if (property.IsCollection)
                    {
                        sourceBuilder.Append(
                            $" _changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value);");
                        sourceBuilder.Append(
                            $" {property.BackingFieldName} = value != null ? new {property.TrackableCollectionType}(value) : null;");
                    }
                    else
                    {
                        sourceBuilder.Append(
                            $" _changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value);");
                        sourceBuilder.Append($" {property.BackingFieldName} = value;");
                    }
                }
                else
                {
                    // For static properties, simply set the value without change tracking
                    if (property.IsCollection)
                        sourceBuilder.Append(
                            $" {property.BackingFieldName} = value != null ? new {property.TrackableCollectionType}(value) : null;");
                    else
                        sourceBuilder.Append($" {property.BackingFieldName} = value;");
                }

                sourceBuilder.AppendLine(" }");
            }
            else
            {
                sourceBuilder.AppendLine($"{fieldIndent}{setterAccessibility}{accessorKeyword};");
            }
        }

        sourceBuilder.AppendLine($"{indent}}}");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Generate the ITrackable interface implementation members
    /// </summary>
    private static void GenerateTrackableImplementation(StringBuilder sourceBuilder, string qualifiedTypeName)
    {
        sourceBuilder.AppendLine(
            $"    private {Constants.IChangeTrackingFullName}<{qualifiedTypeName}>? _changeTracker;");
        sourceBuilder.AppendLine(
            $"    public {Constants.IChangeTrackingFullName}<{qualifiedTypeName}> GetChangeTracker() => _changeTracker ??= new {Constants.ChangeTrackingFullName}<{qualifiedTypeName}>(this);");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Generate a single property implementation
    /// </summary>
    private static void GeneratePropertyImplementation(StringBuilder sourceBuilder, PropertyInfo property)
    {
        // Generate backing field with appropriate type
        var nullableAnnotation = property.IsNullable ? "?" : "";
        var staticModifier = property.IsStatic ? "static " : "";
        var fieldType = property.IsCollection
            ? $"{property.TrackableCollectionType}{nullableAnnotation}"
            : property.TypeName;
        sourceBuilder.AppendLine($"    private {staticModifier}{fieldType} {property.BackingFieldName};");

        // Build property modifiers
        var modifiers = new List<string>
        {
            GetAccessibilityAsString(property.PropertyAccessibility)
        };

        if (property.IsStatic) modifiers.Add("static");
        if (property.IsVirtual) modifiers.Add("virtual");
        if (property.IsOverride) modifiers.Add("override");
        if (property.IsSealed) modifiers.Add("sealed");
        if (property.IsAbstract) modifiers.Add("abstract");
        modifiers.Add("partial");

        // Property declaration
        sourceBuilder.AppendLine($"    {string.Join(" ", modifiers)} {property.TypeName} {property.Name}");
        sourceBuilder.AppendLine("    {");

        // Generate getter if present
        if (property.HasGetter)
        {
            var getterAccessibility = property.GetterAccessibility != property.PropertyAccessibility
                ? $"{GetAccessibilityAsString(property.GetterAccessibility)} "
                : "";

            sourceBuilder.AppendLine(!property.IsAbstract
                ? $"        {getterAccessibility}get => {property.BackingFieldName};"
                : $"        {getterAccessibility}get;");
        }

        // Generate setter if present
        if (property.HasSetter)
        {
            var setterAccessibility = property.SetterAccessibility != property.PropertyAccessibility
                ? $"{GetAccessibilityAsString(property.SetterAccessibility)} "
                : "";

            var accessorKeyword = property.IsSetterInitOnly ? "init" : "set";

            if (!property.IsAbstract)
            {
                sourceBuilder.Append($"        {setterAccessibility}{accessorKeyword}");
                sourceBuilder.Append(" {");

                // Only add change tracking for non-static properties
                if (!property.IsStatic)
                {
                    sourceBuilder.Append(
                        $" _changeTracker?.RecordChange(nameof({property.Name}), {property.BackingFieldName}, value);");
                    // Special handling for collection properties
                    sourceBuilder.Append(property.IsCollection
                        ? $" {property.BackingFieldName} = value != null ? new {property.TrackableCollectionType}(value) : null;"
                        : $" {property.BackingFieldName} = value;");
                }
                else
                {
                    // For static properties, simply set the value without change tracking
                    sourceBuilder.Append(property.IsCollection
                        ? $" {property.BackingFieldName} = value != null ? new {property.TrackableCollectionType}(value) : null;"
                        : $" {property.BackingFieldName} = value;");
                }

                sourceBuilder.AppendLine(" }");
            }
            else
            {
                sourceBuilder.AppendLine($"        {setterAccessibility}{accessorKeyword};");
            }
        }

        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine();
    }

    /// <summary>
    ///     Convert an Accessibility enum value to its string representation
    /// </summary>
    private static string GetAccessibilityAsString(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => string.Empty // Default or NotApplicable
        };
    }

    #endregion
}*/

