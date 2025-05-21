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
        var hasTrackableAttribute = HasAttribute(symbol, Constants.Types.TrackableAttributeFullName);
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
        if (semanticModel.GetDeclaredSymbol(propertyDeclaration) is not { } propertySymbol)
            return;

        var containingType = propertySymbol.ContainingType;
        if (containingType == null) return;

        // Check if the containing type has [Trackable] attribute
        var hasTrackableOnType = HasAttribute(containingType, Constants.Types.TrackableAttributeFullName);
        if (!hasTrackableOnType)
            return;

        var isParial = SymbolHelper.IsPartial(propertySymbol);

        // Identify property tracking attributes
        var hasNotTracked = HasAttribute(propertySymbol, Constants.Types.NotTrackedAttributeFullName);
        var hasTrackOnly = HasAttribute(propertySymbol, Constants.Types.TrackOnlyAttributeFullName);
        var hasTrackCollection = HasAttribute(propertySymbol, Constants.Types.TrackCollectionAttributeFullName);

        // Skip further checks if property is explicitly not tracked
        if (hasNotTracked)
        {
            // CRITICAL RULE 4: Check for conflicting attributes
            if (hasTrackOnly || hasTrackCollection)
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.ConflictingAttributes,
                    propertyDeclaration.Identifier.GetLocation(),
                    propertySymbol.Name));

            return;
        }

        // CRITICAL RULE 2: Check if property with tracking attributes is partial
        if ((hasTrackOnly || hasTrackCollection) && !isParial)
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.PropertyNotPartial,
                propertyDeclaration.Identifier.GetLocation(),
                propertySymbol.Name));

        // CRITICAL RULE 3: Check if TrackCollection is applied to a non-collection type
        if (hasTrackCollection && !IsCollectionType(propertySymbol.Type))
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.TrackCollectionOnNonCollection,
                propertyDeclaration.Identifier.GetLocation(),
                propertySymbol.Name));

        // Determine if this property will be tracked based on tracking mode
        var willBeTracked = false;
        var trackingMode = GetTrackingMode(containingType);

        if (trackingMode == TrackingMode.All)
            // In All mode, only properties with [NotTracked] are excluded
            willBeTracked = isParial && !hasNotTracked;
        else if (trackingMode == TrackingMode.OnlyMarked)
            // In OnlyMarked mode, only properties with [TrackOnly] are included
            willBeTracked = hasTrackOnly;

        // If property will be tracked, check for required setter
        if (willBeTracked)
            // CRITICAL RULE 5: Check if property has a setter
            if (propertySymbol.SetMethod == null)
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.NoSetterOnTrackedProperty,
                    propertyDeclaration.Identifier.GetLocation(),
                    propertySymbol.Name));
    }

    /// <summary>
    ///     Check if a symbol has a specific attribute
    /// </summary>
    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == attributeName);
    }

    /// <summary>
    ///     Get tracking mode from [Trackable] attribute
    /// </summary>
    private static TrackingMode GetTrackingMode(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == Constants.Types.TrackableAttributeFullName)
            {
                // Check constructor arguments
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var value = attribute.ConstructorArguments[0].Value;
                    if (value is int trackingModeValue)
                        return (TrackingMode)trackingModeValue;
                }

                // Check named arguments
                if (attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Mode").Value.Value is int namedValue)
                    return (TrackingMode)namedValue;

                break;
            }
        }

        return TrackingMode.All; // Default if not specified
    }

    /// <summary>
    ///     Check if a type is a collection that can be tracked
    /// </summary>
    private static bool IsCollectionType(ITypeSymbol type)
    {
        // Handle null types
        if (type == null)
            return false;

        // Check if it's a known collection type
        var typeName = type.OriginalDefinition.ToDisplayString();

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
            typeName == "System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>" ||
            typeName == Constants.Types.TrackableListFullName ||
            typeName == Constants.Types.TrackableDictionaryFullName)
            return true;

        // Check if it implements collection interfaces
        if (type is INamedTypeSymbol namedType && namedType.AllInterfaces.Any(i =>
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>" ||
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>" ||
                i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>"))
            return true;

        // Also check for array types
        if (type is IArrayTypeSymbol) return true;

        return false;
    }
}