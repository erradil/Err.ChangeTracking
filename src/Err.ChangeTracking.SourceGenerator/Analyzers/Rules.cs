using Microsoft.CodeAnalysis;

namespace Err.ChangeTracking.SourceGenerator.Analyzers;

public static class Rules
{
    // Define diagnostics for the most critical rules
    private const string TrackableNotPartialId = "ERRTRACK001";
    private const string PropertyNotPartialId = "ERRTRACK002";
    private const string TrackCollectionOnNonCollectionId = "ERRTRACK003";
    private const string ConflictingAttributesId = "ERRTRACK004";
    private const string NoSetterOnTrackedPropertyId = "ERRTRACK005";

    // CRITICAL RULE 1: Classes with [Trackable] must be partial
    public static readonly DiagnosticDescriptor TrackableNotPartial = new(
        TrackableNotPartialId,
        "Trackable class must be partial",
        "Class '{0}' is marked with [Trackable] but is not declared as partial",
        "Err.ChangeTracking",
        DiagnosticSeverity.Error,
        true,
        "Classes marked with [Trackable] must be declared as partial for source generation to work.");

    // CRITICAL RULE 2: Properties with tracking attributes must be partial
    public static readonly DiagnosticDescriptor PropertyNotPartial = new(
        PropertyNotPartialId,
        "Tracking attribute on non-partial property",
        "Property '{0}' has tracking attributes but is not declared as partial",
        "Err.ChangeTracking",
        DiagnosticSeverity.Error,
        true,
        "Properties with [TrackOnly] or [TrackCollection] attributes must be declared as partial for change tracking to work properly.");

    // CRITICAL RULE 3: [TrackCollection] must only be applied to collection types
    public static readonly DiagnosticDescriptor TrackCollectionOnNonCollection = new(
        TrackCollectionOnNonCollectionId,
        "TrackCollection applied to non-collection property",
        "Property '{0}' is marked with [TrackCollection] but is not a collection type",
        "Err.ChangeTracking",
        DiagnosticSeverity.Error,
        true,
        "[TrackCollection] attribute can only be applied to properties of collection types (List<T>, Dictionary<TKey,TValue>, etc.).");

    // CRITICAL RULE 4: Conflicting tracking attributes are not allowed
    public static readonly DiagnosticDescriptor ConflictingAttributes = new(
        ConflictingAttributesId,
        "Conflicting tracking attributes",
        "Property '{0}' has conflicting attributes: [NotTracked] cannot be used with [TrackOnly] or [TrackCollection]",
        "Err.ChangeTracking",
        DiagnosticSeverity.Error,
        true,
        "A property cannot have both [NotTracked] and [TrackOnly]/[TrackCollection] attributes.");

    // CRITICAL RULE 5: Properties must have setters to be trackable
    public static readonly DiagnosticDescriptor NoSetterOnTrackedProperty = new(
        NoSetterOnTrackedPropertyId,
        "Missing setter on tracked property",
        "Property '{0}' marked for tracking has no setter, changes cannot be tracked",
        "Err.ChangeTracking",
        DiagnosticSeverity.Error,
        true,
        "Property marked for tracking has no setter. Changes cannot be tracked.");
}