using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Err.ChangeTracking.SourceGenerator;

[Generator]
public class IncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        //if (!System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Launch();
#endif

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsTrackableClass(node),
                transform: static (ctx, _) => GetClassDeclarationIfHasTrackableAttribute(ctx))
            .Where(static m => m is not null );

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsTrackableClass(SyntaxNode node)
    {
        // Fastest possible rejection - not a class declaration
        if (node is not (ClassDeclarationSyntax or RecordDeclarationSyntax))
            return false;
        if (node is not TypeDeclarationSyntax { AttributeLists.Count: > 0 } typeDecl)
            return false;

        // Fast rejection: not partial - optimized manual check
        if (!HasPartialKeyword(typeDecl.Modifiers))
            return false;

        return HasTrackableAttribute(typeDecl.AttributeLists);
    }

    private static bool HasPartialKeyword(in SyntaxTokenList modifiers)
    {
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PartialKeyword))
                return true;
        }
        return false;
    }

    private static bool HasTrackableAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attributeList in attributeLists)
        {
            var attributes = attributeList.Attributes;
            for (int i = 0; i < attributes.Count; i++)
            {
                if (IsTrackableAttribute(attributes[i].Name))
                    return true;
            }
        }
        return false;
    }

    private static bool IsTrackableAttribute(NameSyntax name)
    {
        // Fast path for simple identifier
        const string attributeName = nameof(TrackableAttribute);
        if (name is IdentifierNameSyntax { Identifier.Text: var text } &&
            (text is "Trackable" or attributeName))
        {
            return true;
        }

        // Fast path for qualified name
        if (name is QualifiedNameSyntax { Right: IdentifierNameSyntax { Identifier.Text: var rightText } } &&
            (rightText is "Trackable" or attributeName))
        {
            return true;
        }

        // Fast path for generic attributes (unlikely but possible)
        if (name is GenericNameSyntax { Identifier.Text: var genericText } &&
            (genericText is "Trackable" or attributeName))
        {
            return true;
        }

        return false;
    }

    private static TrackableClassMetadata? GetClassDeclarationIfHasTrackableAttribute(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl);

        if (typeSymbol == null)
            return null;

        var classMetadata = new TrackableClassMetadata
        {
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = typeSymbol.Name,
            Accessibility = typeSymbol.DeclaredAccessibility,
            IsRecord = typeSymbol.IsRecord,
            IsSealed = typeSymbol.IsSealed,
            IsAbstract = typeSymbol.IsAbstract,
            TypeParameters = typeSymbol.TypeParameters.Select(t => t.Name).ToList(),
            TrackingMode = GetTrackingMode(typeSymbol),
        };

        // Collect all properties
        foreach (var member in classDecl.Members)
        {
            if (member is PropertyDeclarationSyntax propertyDecl)
            {
                var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDecl);
                if (propertySymbol == null) continue;

                if (!IsTrackableProperty(propertySymbol, classMetadata)) continue;

                var propertyType = propertySymbol.Type;

                var wrapperCollectionType = GetWrapperCollectionType(propertyType);

                classMetadata.Properties.Add(new TrackablePropertyMetadata
                {

                    Name = propertySymbol.Name,
                    TypeName = propertySymbol.Type.ToDisplayString(),
                    Accessibility = propertySymbol.DeclaredAccessibility,
                    IsInitOnly = propertySymbol.SetMethod?.IsInitOnly == true,
                    IsStatic = propertySymbol.IsStatic,
                    IsVirtual = propertySymbol.IsVirtual,
                    IsOverride = propertySymbol.IsOverride,
                    IsAbstract = propertySymbol.IsAbstract,
                    FieldName = $"_{Char.ToLowerInvariant(propertySymbol.Name[0])}{propertySymbol.Name.Substring(1)}",
                    IsCollection = wrapperCollectionType is not null,
                    WrapperCollectionType = wrapperCollectionType,
                    
                });
            }
        }

        return classMetadata;
    }

    private static bool IsTrackableProperty(IPropertySymbol propertySymbol, TrackableClassMetadata classMetadata)
    {
        if (!propertySymbol.IsPartialDefinition ||
                   propertySymbol.DeclaredAccessibility != Accessibility.Public ||
                   propertySymbol.IsStatic ||
                   propertySymbol.SetMethod == null)
            return false;

        if (propertySymbol.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(NotTrackedAttribute)))
            return false;

        if (classMetadata.TrackingMode is (int)TrackingMode.OnlyMarked
           && !propertySymbol.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(TrackOnlyAttribute)))
            return false;

        var typeName = propertySymbol.Type.ToDisplayString();
        if (typeName.StartsWith("TrackableList<") || typeName.StartsWith("TrackableDictionary<"))
            return false; // already trackable, skip generation

        return true;
    }

    private static string? GetWrapperCollectionType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var typeDef = namedType.ConstructedFrom?.ToDisplayString();

            var genericArgs = namedType.IsGenericType
                ? string.Join(", ", namedType.TypeArguments.Select(t => t.ToDisplayString()))
                : null;

            if (typeDef?.StartsWith("System.Collections.Generic.Dictionary<") == true)
                return $"TrackableDictionary<{genericArgs}>";

            if (typeDef?.StartsWith("System.Collections.Generic.List<") == true)
                return $"TrackableList<{genericArgs}>";
        }
        return null;
    }

    private static void Execute(Compilation compilation,
    ImmutableArray<TrackableClassMetadata> classes,
    SourceProductionContext context)
    {
        foreach (var classMetadata in classes)
        {
            var source = TemplateCodeHelper.GenerateTrackableClass(classMetadata);
            context.AddSource($"{classMetadata.ClassName}.trackable.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static int? GetTrackingMode(INamedTypeSymbol classSymbol)
    {
        var attr = classSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == nameof(TrackableAttribute));
        if (attr?.NamedArguments.FirstOrDefault(kvp => kvp.Key == nameof(TrackableAttribute.Mode)).Value.Value is int mode)
            return mode;
        return null;
    }
}