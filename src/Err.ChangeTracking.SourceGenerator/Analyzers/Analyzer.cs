using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Err.ChangeTracking.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Analyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     Only the most critical diagnostics are enabled to enforce strict validation rules
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        // CRITICAL RULE 1: Classes with [Trackable] must be partial
        Rules.TrackableNotPartial,

        // CRITICAL RULE 2: Properties with tracking attributes must be partial
        Rules.PropertyNotPartial,

        // CRITICAL RULE 3: [TrackCollection] must only be applied to collection types
        Rules.TrackCollectionOnNonCollection,

        // CRITICAL RULE 4: Conflicting tracking attributes are not allowed
        Rules.ConflictingAttributes,

        // CRITICAL RULE 5: Properties must have setters to be trackable
        Rules.NoSetterOnTrackedProperty
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register actions for analyzing syntax
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>
    ///     Analyze type declarations to check if they are marked as partial when using [Trackable]
    /// </summary>
    private void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get type symbol
        var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol == null) return;

        // Only analyze types with [Trackable] attribute
        var hasTrackableAttribute = PropertyHelper.HasTrackableAttribute(symbol);
        if (!hasTrackableAttribute)
            return;

        // CRITICAL RULE 1: Check if the type is partial
        if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.TrackableNotPartial,
                typeDeclaration.Identifier.GetLocation(),
                symbol.Name));
        }
    }

    /// <summary>
    ///     Analyze property declarations to check for tracking-related issues
    /// </summary>
    private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get property symbol and containing type
        if (semanticModel.GetDeclaredSymbol(propertyDeclaration) is not IPropertySymbol propertySymbol)
            return;

        var containingType = propertySymbol.ContainingType;
        if (containingType == null) return;

        // Check if the containing type has [Trackable] attribute
        if (!PropertyHelper.HasTrackableAttribute(containingType))
            return;

        // Create PropertyHelper for the property
        var propertyHelper = new PropertyHelper(propertySymbol);


        // CRITICAL RULE 4: Check for conflicting attributes
        if (propertyHelper.HasNotTrackedAttribute &&
            (propertyHelper.HasTrackOnlyAttribute || propertyHelper.HasTrackCollectionAttribute))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.ConflictingAttributes,
                propertyDeclaration.Identifier.GetLocation(),
                propertyHelper.Name));

            return; // Skip further checks if property has conflicting attributes
        }

        // CRITICAL RULE 2: Check if property with tracking attributes is partial
        if ((propertyHelper.HasTrackOnlyAttribute || propertyHelper.HasTrackCollectionAttribute) &&
            !propertyHelper.IsPartial())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.PropertyNotPartial,
                propertyDeclaration.Identifier.GetLocation(),
                propertyHelper.Name));
        }

        // CRITICAL RULE 3: Check if TrackCollection is applied to a non-collection type
        if (propertyHelper.HasTrackCollectionAttribute)
        {
            var (isCollection, _) = IsCollectionType(propertySymbol.Type);
            if (!isCollection)
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.TrackCollectionOnNonCollection,
                    propertyDeclaration.Identifier.GetLocation(),
                    propertyHelper.Name));
        }

        // Determine if this property will be tracked based on tracking mode
        var willBeTracked = false;
        var trackingMode = PropertyHelper.GetTrackingMode(containingType);

        if (trackingMode == TrackingMode.All)
            // In All mode, only properties with [NotTracked] are excluded
            willBeTracked = propertyHelper.IsPartial() && !propertyHelper.HasNotTrackedAttribute;
        else if (trackingMode == TrackingMode.OnlyMarked)
            // In OnlyMarked mode, only properties with [TrackOnly] are included
            willBeTracked = propertyHelper.HasTrackOnlyAttribute;

        // CRITICAL RULE 5: Check if property has a setter when it's going to be tracked
        if (willBeTracked && !propertyHelper.HasSetter)
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.NoSetterOnTrackedProperty,
                propertyDeclaration.Identifier.GetLocation(),
                propertyHelper.Name));
    }

    /// <summary>
    ///     Check if a type is a collection that can be tracked
    /// </summary>
    private static (bool isCollection, bool isTrackable) IsCollectionType(ITypeSymbol type)
    {
        // Handle null types
        if (type == null)
            return (false, false);

        // Check if it's a known collection type
        var typeName = type.OriginalDefinition.ToDisplayString();

        // Check if it's already a trackable collection
        if (typeName.StartsWith(Constants.Types.TrackableListFullName) ||
            typeName.StartsWith(Constants.Types.TrackableDictionaryFullName))
            return (true, true);

        // Check for exact matches with common collection types
        if (typeName == "System.Collections.Generic.List<T>" ||
            typeName == "System.Collections.Generic.Dictionary<TKey, TValue>" ||
            typeName == "System.Collections.Generic.HashSet<T>" ||
            typeName == "System.Collections.Generic.SortedSet<T>" ||
            typeName == "System.Collections.Generic.Queue<T>" ||
            typeName == "System.Collections.Generic.Stack<T>" ||
            typeName == "System.Collections.ObjectModel.Collection<T>" ||
            typeName == "System.Collections.ObjectModel.ObservableCollection<T>" ||
            typeName == "System.Collections.Concurrent.ConcurrentBag<T>" ||
            typeName == "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>")
            return (true, false);

        // Check if it implements collection interfaces
        if (type is INamedTypeSymbol namedType && namedType.AllInterfaces.Any(i =>
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>" ||
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>" ||
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>"))
            return (true, false);

        // Also check for array types
        if (type is IArrayTypeSymbol)
            return (true, false);

        return (false, false);
    }
}