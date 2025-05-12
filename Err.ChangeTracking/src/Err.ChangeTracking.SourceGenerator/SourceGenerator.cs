using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Err.ChangeTracking.SourceGenerator
{
    [Generator]
    public class PartialPropertyGenerator : IIncrementalGenerator
    {
        private const string Namespace = "Err.ChangeTracking";
        private const string TrackableAttributeFullName = $"{Namespace}.TrackableAttribute";
        private const string ITrackableFullName = $"{Namespace}.ITrackable";
        private const string IChangeTrackingFullName = $"{Namespace}.IChangeTracking";
        private const string ChangeTrackingFullName = $"{Namespace}.ChangeTracking";
        private const string TrackCollectionAttributeFullName = $"{Namespace}.TrackCollectionAttribute";
        private const string TrackableListFullName = $"{Namespace}.TrackableList";
        private const string TrackableDictionaryFullName = $"{Namespace}.TrackableDictionary";
        private const string TrackOnlyAttributeFullName = $"{Namespace}.TrackOnlyAttribute";
        private const string NotTrackedAttributeFullName = $"{Namespace}.NotTrackedAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Filter for trackable type declarations with partial properties
            IncrementalValuesProvider<TypeDeclarationSyntax> trackableTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsPotentialTrackableType(s),
                    transform: static (ctx, _) => GetTrackableType(ctx))
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
        /// Initial syntactic filter for potential trackable types
        /// </summary>
        private static bool IsPotentialTrackableType(SyntaxNode node)
        {
            // Must be a class, struct, or record declaration
            if (node is not TypeDeclarationSyntax typeDecl)
            {
                return false;
            }

            // Must have the 'partial' modifier
            bool isPartial = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
            if (!isPartial)
            {
                return false;
            }

            // Must have at least one attribute list
            if (typeDecl.AttributeLists.Count == 0)
            {
                return false;
            }

            // Must have at least one partial property
            bool hasPartialProperty = typeDecl.Members.OfType<PropertyDeclarationSyntax>()
                .Any(p => p.Modifiers.Any(SyntaxKind.PartialKeyword));

            return hasPartialProperty;
        }

        /// <summary>
        /// Semantic filter to confirm a type has the [Trackable] attribute
        /// </summary>
        private static TypeDeclarationSyntax? GetTrackableType(GeneratorSyntaxContext context)
        {
            var typeDeclarationSyntax = (TypeDeclarationSyntax)context.Node;

            if (HasTrackableAttribute(context, typeDeclarationSyntax))
            {
                return typeDeclarationSyntax;
            }

            return null;
        }

        /// <summary>
        /// Check if the type declaration has the [Trackable] attribute
        /// </summary>
        private static bool HasTrackableAttribute(GeneratorSyntaxContext context,
            TypeDeclarationSyntax typeDeclarationSyntax)
        {
            foreach (AttributeListSyntax attributeListSyntax in typeDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attributeSyntax)
                            .Symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    string attributeFullName = attributeSymbol.ContainingType.ToDisplayString();
                    if (attributeFullName == TrackableAttributeFullName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Find all trackable partial properties in a type declaration based on tracking rules
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
                {
                    continue;
                }

                var propertySymbol = semanticModel.GetDeclaredSymbol(propertySyntax);
                if (propertySymbol == null) continue;

                // Check tracking attributes
                var isTrackOnly = HasAttribute(propertySymbol, TrackOnlyAttributeFullName);
                var isNotTracked = HasAttribute(propertySymbol, NotTrackedAttributeFullName);

                // Check if already a trackable collection
                var isTrackableCollection = IsTrackableCollection(propertySymbol);

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
        ///     Check if a type is already a trackable collection
        /// </summary>
        private static bool IsTrackableCollection(IPropertySymbol propertySymbol)
        {
            var typeName = propertySymbol.Type.ToDisplayString();

            // No need to track the types is TrackableList or TrackableDictionary, they are by default trackable
            if (typeName.StartsWith(TrackableListFullName) ||
                typeName.StartsWith(TrackableDictionaryFullName))
                return false;
            // Check if the property has TrackCollectionAttribute
            var isTrackCollection = HasAttribute(propertySymbol, TrackCollectionAttributeFullName);

            return isTrackCollection
                   && (typeName.StartsWith("System.Collections.Generic.List<T>") ||
                       typeName.StartsWith("System.Collections.Generic.Dictionary<TKey, TValue>"));
        }

        /// <summary>
        ///     Get tracking mode from the [Trackable] attribute
        /// </summary>
        private static TrackingMode GetTrackingMode(INamedTypeSymbol? typeSymbol)
        {
            if (typeSymbol == null) return TrackingMode.All; // Default

            foreach (var attribute in typeSymbol.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == TrackableAttributeFullName)
                {
                    // Check if attribute has constructor arguments for tracking mode
                    if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Mode").Value.Value is int
                        trackingModeValue)
                        return (TrackingMode)trackingModeValue;

                    break;
                }

            return TrackingMode.All; // Default if not specified
        }

        /// <summary>
        ///     Check if a property has a specific attribute
        /// </summary>
        private static bool HasAttribute(IPropertySymbol propertySymbol, string attributeFullName)
        {
            foreach (var attribute in propertySymbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == attributeFullName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Tracking modes supported by [Trackable] attribute adil
        /// </summary>
        private enum TrackingMode
        {
            All,
            OnlyMarked
        }

        /// <summary>
        /// Check if a type implements ITrackable<T> interface
        /// </summary>
        private static bool ImplementsTrackableInterface(INamedTypeSymbol typeSymbol)
        {
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                if (interfaceSymbol.OriginalDefinition.ToDisplayString().StartsWith(ITrackableFullName))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Property Information Extraction

        /// <summary>
        /// Represents all metadata needed to generate a property implementation
        /// </summary>
        private class PropertyInfo
        {
            public string Name { get; }
            public string TypeName { get; }
            public string BackingFieldName { get; }
            public bool IsStatic { get; }
            public bool IsVirtual { get; }
            public bool IsOverride { get; }
            public bool IsAbstract { get; }
            public bool IsSealed { get; }
            public Accessibility PropertyAccessibility { get; }

            // Accessor information
            public bool HasGetter { get; }
            public bool HasSetter { get; }
            public bool IsSetterInitOnly { get; }
            public Accessibility GetterAccessibility { get; }
            public Accessibility SetterAccessibility { get; }

            // Collection tracking information
            public bool IsCollection { get; }
            public string? TrackableCollectionType { get; }
            public bool IsNullable { get; }

            public PropertyInfo(IPropertySymbol propertySymbol)
            {
                Name = propertySymbol.Name;
                TypeName = propertySymbol.Type.ToDisplayString();
                BackingFieldName = $"_{char.ToLowerInvariant(Name[0])}{Name.Substring(1)}";

                // Modifiers
                IsStatic = propertySymbol.IsStatic;
                IsVirtual = propertySymbol.IsVirtual;
                IsOverride = propertySymbol.IsOverride;
                IsAbstract = propertySymbol.IsAbstract;
                IsSealed = propertySymbol.IsSealed;
                PropertyAccessibility = propertySymbol.DeclaredAccessibility;

                // Accessors
                HasGetter = propertySymbol.GetMethod != null;
                HasSetter = propertySymbol.SetMethod != null;
                IsSetterInitOnly = HasSetter && propertySymbol.SetMethod!.IsInitOnly;
                GetterAccessibility =
                    HasGetter ? propertySymbol.GetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
                SetterAccessibility =
                    HasSetter ? propertySymbol.SetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
                // Check if the type is nullable
                IsNullable = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated;

                // Check if this is a trackable collection
                var collectionInfo = GetTrackableCollectionInfo(propertySymbol);
                IsCollection = collectionInfo.IsCollection;
                TrackableCollectionType = collectionInfo.TrackableType;
            }


            /// <summary>
            ///     Check if a property has the [TrackCollection] attribute
            /// </summary>
            private static bool HasTrackCollectionAttribute(IPropertySymbol propertySymbol)
            {
                foreach (var attribute in propertySymbol.GetAttributes())
                    if (attribute.AttributeClass?.ToDisplayString() == TrackCollectionAttributeFullName)
                        return true;

                return false;
            }

            /// <summary>
            ///     Check if a property type is a collection that should be converted to a trackable version
            /// </summary>
            private static (bool IsCollection, string TrackableType) GetTrackableCollectionInfo(
                IPropertySymbol property)
            {
                if (!HasTrackCollectionAttribute(property))
                    return (false, string.Empty);

                var propertyType = property.Type;


                // Check for List<T>
                if (propertyType is INamedTypeSymbol listType &&
                    listType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>")
                {
                    var elementType = listType.TypeArguments[0].ToDisplayString();
                    return (true, $"{TrackableListFullName}<{elementType}>");
                }

                // Check for Dictionary<K,V>
                if (propertyType is INamedTypeSymbol dictType &&
                    dictType.OriginalDefinition.ToDisplayString() ==
                    "System.Collections.Generic.Dictionary<TKey, TValue>")
                {
                    var keyType = dictType.TypeArguments[0].ToDisplayString();
                    var valueType = dictType.TypeArguments[1].ToDisplayString();
                    return (true, $"{TrackableDictionaryFullName}<{keyType}, {valueType}>");
                }

                return (false, string.Empty);
            }
        }

        /// <summary>
        /// Extract property information from a property symbol
        /// </summary>
        private static PropertyInfo ExtractPropertyInfo(IPropertySymbol propertySymbol)
        {
            return new PropertyInfo(propertySymbol);
        }

        #endregion

        #region Source Generation

        /// <summary>
        /// Main source generation method
        /// </summary>
        private static void GenerateTrackableImplementations(
            Compilation compilation,
            ImmutableArray<TypeDeclarationSyntax> types,
            SourceProductionContext context)
        {
            if (types.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var typeSyntax in types.Distinct())
            {
                var semanticModel = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax);

                if (typeSymbol == null)
                {
                    continue;
                }

                var typeInfo = ExtractTypeInfo(typeSymbol);
                var properties = GetTrackableProperties(typeSyntax, semanticModel)
                    .Select(p => ExtractPropertyInfo(p.Symbol))
                    .ToList();

                if (properties.Count > 0)
                {
                    var generatedSource = GeneratePartialTypeWithProperties(typeInfo, properties,
                        ImplementsTrackableInterface(typeSymbol));
                    var fileName = $"{typeInfo.GetFullName()}.g.cs";
                    context.AddSource(fileName, SourceText.From(generatedSource, Encoding.UTF8));
                }
            }
        }

        /// <summary>
        /// Represents metadata about a type needed for generation
        /// </summary>
        private class TypeInfo
        {
            public string Name { get; }
            public string? Namespace { get; }
            public string Kind { get; } // class, struct, record, record struct
            public Accessibility Accessibility { get; }
            public List<string> Modifiers { get; } = [];
            public List<string> ContainingTypes { get; } = [];
            public List<ContainingTypeInfo> ContainingTypeInfos { get; } = [];

            public TypeInfo(INamedTypeSymbol typeSymbol)
            {
                Name = typeSymbol.Name;
                Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : typeSymbol.ContainingNamespace.ToDisplayString();
                Kind = DetermineTypeKind(typeSymbol);
                Accessibility = typeSymbol.DeclaredAccessibility;

                // Extract modifiers
                if (typeSymbol.IsStatic) Modifiers.Add("static");
                if (typeSymbol.IsAbstract && typeSymbol.TypeKind != TypeKind.Interface) Modifiers.Add("abstract");
                if (typeSymbol.IsSealed && !typeSymbol.IsValueType && !typeSymbol.IsRecord) Modifiers.Add("sealed");

                // Get containing types
                var current = typeSymbol.ContainingType;
                while (current != null)
                {
                    ContainingTypes.Insert(0, current.Name); // Add at beginning to maintain hierarchy

                    var containingTypeInfo = new ContainingTypeInfo(
                        current.Name,
                        DetermineTypeKind(current),
                        current.DeclaredAccessibility,
                        current.IsStatic,
                        current.IsAbstract && current.TypeKind != TypeKind.Interface,
                        current.IsSealed && !current.IsValueType && !current.IsRecord
                    );

                    ContainingTypeInfos.Insert(0, containingTypeInfo);
                    current = current.ContainingType;
                }
            }

            /// <summary>
            /// Returns the fully qualified name of the type including namespace and any containing types
            /// </summary>
            public string GetFullName()
            {
                var sb = new StringBuilder();

                // Start with the namespace if not global
                if (!string.IsNullOrEmpty(Namespace)) sb.Append(Namespace);

                // Add all containing types in order
                foreach (var containingType in ContainingTypes)
                {
                    if (sb.Length > 0)
                        sb.Append('.');
                    sb.Append(containingType);
                }

                // Add the type name itself
                if (sb.Length > 0)
                    sb.Append('.');
                sb.Append(Name);

                return sb.ToString();
            }
        }

        /// <summary>
        /// Information about a containing/parent type
        /// </summary>
        private class ContainingTypeInfo
        {
            public string Name { get; }
            public string Kind { get; }
            public Accessibility Accessibility { get; }
            public bool IsStatic { get; }
            public bool IsAbstract { get; }
            public bool IsSealed { get; }
            public List<string> Modifiers { get; } = [];

            public ContainingTypeInfo(string name, string kind, Accessibility accessibility, bool isStatic,
                bool isAbstract, bool isSealed)
            {
                Name = name;
                Kind = kind;
                Accessibility = accessibility;
                IsStatic = isStatic;
                IsAbstract = isAbstract;
                IsSealed = isSealed;

                // Build modifiers list
                if (isStatic) Modifiers.Add("static");
                if (isAbstract) Modifiers.Add("abstract");
                if (isSealed) Modifiers.Add("sealed");
            }
        }

        /// <summary>
        /// Extract type information from a type symbol
        /// </summary>
        private static TypeInfo ExtractTypeInfo(INamedTypeSymbol typeSymbol)
        {
            return new TypeInfo(typeSymbol);
        }

        /// <summary>
        /// Determine the kind of a type (class, struct, record, record struct)
        /// </summary>
        private static string DetermineTypeKind(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.IsValueType)
            {
                return typeSymbol.IsRecord ? "record struct" : "struct";
            }

            return typeSymbol.IsRecord ? "record" : "class";
        }

        /// <summary>
        /// Gets the containing type names in the format "OuterType.MiddleType"
        /// </summary>
        private static string GetContainingTypeNames(TypeInfo typeInfo)
        {
            if (typeInfo.ContainingTypes.Count == 0)
                return string.Empty;

            return string.Join(".", typeInfo.ContainingTypes);
        }

        /// <summary>
        /// Generate the entire partial type implementation with its properties
        /// </summary>
        private static string GeneratePartialTypeWithProperties(TypeInfo typeInfo, List<PropertyInfo> properties,
            bool alreadyImplementsTrackable)
        {
            var sourceBuilder = new StringBuilder();

            // Add namespace if needed
            if (typeInfo.Namespace != null)
            {
                sourceBuilder.AppendLine($"#nullable enable");
                sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};");
                sourceBuilder.AppendLine();
            }

            // For nested types, we need to generate a nested hierarchy
            if (typeInfo.ContainingTypes.Count > 0)
            {
                GenerateNestedTypeHierarchy(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);
            }
            else
            {
                // For non-nested types, generate the standard way
                GenerateStandardType(sourceBuilder, typeInfo, properties, alreadyImplementsTrackable);
            }

            return sourceBuilder.ToString();
        }

        /// <summary>
        /// Generate a standard (non-nested) type
        /// </summary>
        private static void GenerateStandardType(StringBuilder sourceBuilder, TypeInfo typeInfo,
            List<PropertyInfo> properties, bool alreadyImplementsTrackable)
        {
            sourceBuilder.AppendLine($"// Auto-generated for {typeInfo.Name} due to [TrackableAttribute]");

            // Build type declaration with all modifiers
            string accessibility = GetAccessibilityAsString(typeInfo.Accessibility);
            string modifiers = string.Join(" ", typeInfo.Modifiers);
            string modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";

            // Add the ITrackable<T> interface if not already implemented
            string qualifiedTypeName = typeInfo.Name;
            var interfaceImplementation = !alreadyImplementsTrackable
                ? $" : {ITrackableFullName}<{qualifiedTypeName}>"
                : "";

            sourceBuilder.AppendLine(
                $"{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
            sourceBuilder.AppendLine("{");

            // If we're adding the interface implementation, generate the required members
            if (!alreadyImplementsTrackable)
            {
                GenerateTrackableImplementation(sourceBuilder, qualifiedTypeName);
            }

            // Generate each property
            foreach (var property in properties)
            {
                GeneratePropertyImplementation(sourceBuilder, property);
            }

            sourceBuilder.AppendLine("}");
        }

        /// <summary>
        /// Generate nested type hierarchy for embedded types
        /// </summary>
        private static void GenerateNestedTypeHierarchy(StringBuilder sourceBuilder, TypeInfo typeInfo,
            List<PropertyInfo> properties, bool alreadyImplementsTrackable)
        {
            // Get fully qualified type name for interface implementation
            string containingTypeNames = GetContainingTypeNames(typeInfo);
            string qualifiedTypeName = $"{containingTypeNames}.{typeInfo.Name}";

            // Generate the containing type declarations (outer to inner)
            int indent = 0;

            // For each containing type, open a partial type declaration
            for (int i = 0; i < typeInfo.ContainingTypeInfos.Count; i++)
            {
                var containingType = typeInfo.ContainingTypeInfos[i];
                string indentStr = new string(' ', indent * 4);

                string containingAccessibility = GetAccessibilityAsString(containingType.Accessibility);
                string containingModifiers = string.Join(" ", containingType.Modifiers);
                var containingModifiersWithSpace =
                    !string.IsNullOrEmpty(containingModifiers) ? containingModifiers + " " : "";

                sourceBuilder.AppendLine(
                    $"{indentStr}{containingAccessibility} {containingModifiersWithSpace}partial {containingType.Kind} {containingType.Name}");
                sourceBuilder.AppendLine($"{indentStr}{{");

                indent++;
            }

            // Now generate the actual type we're tracking
            string typeIndent = new string(' ', indent * 4);

            // Build type declaration with all modifiers
            string accessibility = GetAccessibilityAsString(typeInfo.Accessibility);
            string modifiers = string.Join(" ", typeInfo.Modifiers);
            string modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";

            // Add the ITrackable<T> interface if not already implemented
            var interfaceImplementation = !alreadyImplementsTrackable
                ? $" : {ITrackableFullName}<{qualifiedTypeName}>"
                : "";

            sourceBuilder.AppendLine($"{typeIndent}// Auto-generated for {typeInfo.Name} due to [TrackableAttribute]");
            sourceBuilder.AppendLine(
                $"{typeIndent}{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
            sourceBuilder.AppendLine($"{typeIndent}{{");

            indent++;

            // If we're adding the interface implementation, generate the required members with proper indentation
            if (!alreadyImplementsTrackable)
            {
                string memberIndent = new string(' ', indent * 4);

                sourceBuilder.AppendLine(
                    $"{memberIndent}private {IChangeTrackingFullName}<{qualifiedTypeName}>? _changeTracker;");
                sourceBuilder.AppendLine(
                    $"{memberIndent}public {IChangeTrackingFullName}<{qualifiedTypeName}> GetChangeTracker() => _changeTracker ??= new {ChangeTrackingFullName}<{qualifiedTypeName}>(this);");
                sourceBuilder.AppendLine();
            }

            // Generate each property with proper indentation
            foreach (var property in properties)
            {
                GeneratePropertyImplementationWithIndent(sourceBuilder, property, indent);
            }

            // Close type and all containing types
            for (int i = indent; i > 0; i--)
            {
                string closeIndent = new string(' ', (i - 1) * 4);
                sourceBuilder.AppendLine($"{closeIndent}}}");
            }
        }

        /// <summary>
        /// Generate a property implementation with custom indentation
        /// </summary>
        private static void GeneratePropertyImplementationWithIndent(StringBuilder sourceBuilder, PropertyInfo property,
            int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);
            string fieldIndent = new string(' ', (indentLevel + 1) * 4);
            var codeIndent = new string(' ', (indentLevel + 2) * 4);

            var nullableAnnotation = property.IsNullable ? "?" : "";

            // Generate backing field with appropriate type
            string staticModifier = property.IsStatic ? "static " : "";
            var fieldType = property.IsCollection
                ? $"{property.TrackableCollectionType}{nullableAnnotation}"
                : property.TypeName;
            sourceBuilder.AppendLine($"{indent}private {staticModifier}{fieldType} {property.BackingFieldName};");

            // Build property modifiers
            List<string> modifiers = new List<string>
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
                string getterAccessibility = property.GetterAccessibility != property.PropertyAccessibility
                    ? $"{GetAccessibilityAsString(property.GetterAccessibility)} "
                    : "";

                if (!property.IsAbstract)
                {
                    sourceBuilder.AppendLine($"{fieldIndent}{getterAccessibility}get => {property.BackingFieldName};");
                }
                else
                {
                    sourceBuilder.AppendLine($"{fieldIndent}{getterAccessibility}get;");
                }
            }

            // Generate setter if present
            if (property.HasSetter)
            {
                string setterAccessibility = property.SetterAccessibility != property.PropertyAccessibility
                    ? $"{GetAccessibilityAsString(property.SetterAccessibility)} "
                    : "";

                string accessorKeyword = property.IsSetterInitOnly ? "init" : "set";

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
        /// Generate the ITrackable interface implementation members
        /// </summary>
        private static void GenerateTrackableImplementation(StringBuilder sourceBuilder, string qualifiedTypeName)
        {
            sourceBuilder.AppendLine(
                $"    private {IChangeTrackingFullName}<{qualifiedTypeName}>? _changeTracker;");
            sourceBuilder.AppendLine(
                $"    public {IChangeTrackingFullName}<{qualifiedTypeName}> GetChangeTracker() => _changeTracker ??= new {ChangeTrackingFullName}<{qualifiedTypeName}>(this);");
            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Generate a single property implementation
        /// </summary>
        private static void GeneratePropertyImplementation(StringBuilder sourceBuilder, PropertyInfo property)
        {
            // Generate backing field with appropriate type
            var nullableAnnotation = property.IsNullable ? "?" : "";
            string staticModifier = property.IsStatic ? "static " : "";
            var fieldType = property.IsCollection
                ? $"{property.TrackableCollectionType}{nullableAnnotation}"
                : property.TypeName;
            sourceBuilder.AppendLine($"    private {staticModifier}{fieldType} {property.BackingFieldName};");

            // Build property modifiers
            List<string> modifiers = new List<string>
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
        /// Convert an Accessibility enum value to its string representation
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
    }
}