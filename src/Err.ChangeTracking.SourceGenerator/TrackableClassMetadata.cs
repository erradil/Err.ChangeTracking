using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Err.ChangeTracking.SourceGenerator;

public class TrackableClassMetadata
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public Accessibility Accessibility { get; set; }
    public bool IsRecord { get; set; }
    public bool IsSealed { get; set; }
    public bool IsAbstract { get; set; }
    public List<string> TypeParameters { get; set; } = new();
    public List<TrackablePropertyMetadata> Properties { get; set; } = [];
    public int? TrackingMode { get; set; }
}

public record TrackablePropertyMetadata
{
    public string FieldName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public Accessibility Accessibility { get; set; }
    public bool IsInitOnly { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsCollection { get; set; }
    public string? WrapperCollectionType { get; set; } = string.Empty;

}