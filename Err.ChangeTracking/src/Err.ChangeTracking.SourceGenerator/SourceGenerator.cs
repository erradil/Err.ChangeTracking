using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Err.ChangeTracking.SourceGenerator
{
    [Generator]
    public class PartialPropertyGenerator : IIncrementalGenerator
    {
        private const string TrackableAttributeFullName = "Err.ChangeTracking.TrackableAttribute";
        private const string TrackableInterfaceFullName = "Err.ChangeTracking.ITrackable";
        private const string ChangeTrackingInterfaceFullName = "Err.ChangeTracking.IChangeTracking";
        private const string ChangeTrackingClassFullName = "Err.ChangeTracking.ChangeTracking";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Filter for trackable type declarations with partial properties
            IncrementalValuesProvider<TypeDeclarationSyntax> trackableTypes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsPotentialTrackableType(s),
                    transform: static (ctx, _) => GetTrackableType(ctx))
                .Where(static m => m is not null)!;

            // Combine the compilation with our filtered type declarations
            IncrementalValueProvider<(Compilation, System.Collections.Immutable.ImmutableArray<TypeDeclarationSyntax>)> compilationAndTypes =
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
        private static bool HasTrackableAttribute(GeneratorSyntaxContext context, TypeDeclarationSyntax typeDeclarationSyntax)
        {
            foreach (AttributeListSyntax attributeListSyntax in typeDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
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
        /// Find all partial properties in a type declaration
        /// </summary>
        private static IEnumerable<(PropertyDeclarationSyntax Syntax, IPropertySymbol Symbol)> GetTrackableProperties(
            TypeDeclarationSyntax typeSyntax, SemanticModel semanticModel)
        {
            foreach (var member in typeSyntax.Members)
            {
                if (member is PropertyDeclarationSyntax propertySyntax &&
                    propertySyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    var propertySymbol = semanticModel.GetDeclaredSymbol(propertySyntax);
                    if (propertySymbol != null)
                    {
                        yield return (propertySyntax, propertySymbol);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a type implements ITrackable<T> interface
        /// </summary>
        private static bool ImplementsTrackableInterface(INamedTypeSymbol typeSymbol)
        {
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                if (interfaceSymbol.OriginalDefinition.ToDisplayString().StartsWith(TrackableInterfaceFullName))
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
                GetterAccessibility = HasGetter ? propertySymbol.GetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
                SetterAccessibility = HasSetter ? propertySymbol.SetMethod!.DeclaredAccessibility : Accessibility.NotApplicable;
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
            System.Collections.Immutable.ImmutableArray<TypeDeclarationSyntax> types, 
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
                    string generatedSource = GeneratePartialTypeWithProperties(typeInfo, properties, ImplementsTrackableInterface(typeSymbol));
                    string fileName = $"{typeSymbol.ContainingNamespace.ToDisplayString()}.{typeSymbol.Name}.g.cs";
                    context.AddSource(fileName, SourceText.From(generatedSource, System.Text.Encoding.UTF8));
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
            public List<string> Modifiers { get; } = new List<string>();

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
        /// Generate the entire partial type implementation with its properties
        /// </summary>
        private static string GeneratePartialTypeWithProperties(TypeInfo typeInfo, List<PropertyInfo> properties, bool alreadyImplementsTrackable)
        {
            var sourceBuilder = new StringBuilder();

            // Add namespace if needed
            if (typeInfo.Namespace != null)
            {
                sourceBuilder.AppendLine($"#nullable enable");
                sourceBuilder.AppendLine($"namespace {typeInfo.Namespace};");
                sourceBuilder.AppendLine();
            }

            // Type declaration
            sourceBuilder.AppendLine($"// Auto-generated for {typeInfo.Name} due to [TrackableAttribute]");
            
            // Build type declaration with all modifiers
            string accessibility = GetAccessibilityAsString(typeInfo.Accessibility);
            string modifiers = string.Join(" ", typeInfo.Modifiers);
            string modifiersWithSpace = !string.IsNullOrEmpty(modifiers) ? modifiers + " " : "";
            
            // Add the ITrackable<T> interface if not already implemented
            string interfaceImplementation = !alreadyImplementsTrackable ? 
                $" : {TrackableInterfaceFullName}<{typeInfo.Name}>" : "";
            
            sourceBuilder.AppendLine($"{accessibility} {modifiersWithSpace}partial {typeInfo.Kind} {typeInfo.Name}{interfaceImplementation}");
            sourceBuilder.AppendLine("{");

            // If we're adding the interface implementation, generate the required members
            if (!alreadyImplementsTrackable)
            {
                GenerateTrackableImplementation(sourceBuilder, typeInfo.Name);
            }

            // Generate each property
            foreach (var property in properties)
            {
                GeneratePropertyImplementation(sourceBuilder, property);
            }

            sourceBuilder.AppendLine("}");
            return sourceBuilder.ToString();
        }

        /// <summary>
        /// Generate the ITrackable interface implementation members
        /// </summary>
        private static void GenerateTrackableImplementation(StringBuilder sourceBuilder, string typeName)
        {
            sourceBuilder.AppendLine($"    private {ChangeTrackingInterfaceFullName}<{typeName}>? _changeTracker;");
            sourceBuilder.AppendLine($"    public {ChangeTrackingInterfaceFullName}<{typeName}> GetChangeTracker() => _changeTracker ??= new {ChangeTrackingClassFullName}<{typeName}>(this);");
            sourceBuilder.AppendLine();
        }

        /// <summary>
        /// Generate a single property implementation
        /// </summary>
        private static void GeneratePropertyImplementation(StringBuilder sourceBuilder, PropertyInfo property)
        {
            // Generate backing field
            string staticModifier = property.IsStatic ? "static " : "";
            sourceBuilder.AppendLine($"    private {staticModifier}{property.TypeName} {property.BackingFieldName};");

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
                string getterAccessibility = property.GetterAccessibility != property.PropertyAccessibility
                    ? $"{GetAccessibilityAsString(property.GetterAccessibility)} "
                    : "";
                
                if (!property.IsAbstract)
                {
                    sourceBuilder.AppendLine($"        {getterAccessibility}get => {property.BackingFieldName};");
                }
                else
                {
                    sourceBuilder.AppendLine($"        {getterAccessibility}get;");
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
                    sourceBuilder.Append($"        {setterAccessibility}{accessorKeyword}");
                    sourceBuilder.Append(" {");
                    
                    // Only add change tracking for non-static properties
                    if (!property.IsStatic)
                    {
                        sourceBuilder.Append($" _changeTracker?.RecordChange(\"{property.Name}\", {property.BackingFieldName}, value);");
                        sourceBuilder.Append($" {property.BackingFieldName} = value;");
                    }
                    else
                    {
                        // For static properties, simply set the value without change tracking
                        sourceBuilder.Append( $" {property.BackingFieldName} = value;");
                    }
                    
                    sourceBuilder.AppendLine(" }");
                }
                else
                {
                    sourceBuilder.AppendLine($"{setterAccessibility}{accessorKeyword};");
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