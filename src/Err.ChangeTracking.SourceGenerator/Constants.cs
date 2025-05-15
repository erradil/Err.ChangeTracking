namespace Err.ChangeTracking.SourceGenerator;

public static class Constants
{
    public static class Keywords
    {
        public const string PartialKeyword = "partial";
        public const string StaticKeyword = "static";
        public const string GetAccessor = "get";
        public const string SetAccessor = "set";
        public const string InitAccessor = "init";
    }

    public const string Namespace = "Err.ChangeTracking";

    public static class Types
    {
        public const string TrackableAttributeFullName = $"{Namespace}.TrackableAttribute";
        public const string ITrackableFullName = $"{Namespace}.ITrackable";
        public const string IChangeTrackingFullName = $"{Namespace}.IChangeTracking";
        public const string ChangeTrackingFullName = $"{Namespace}.ChangeTracking";
        public const string TrackCollectionAttributeFullName = $"{Namespace}.TrackCollectionAttribute";
        public const string TrackableListFullName = $"{Namespace}.TrackableList";
        public const string TrackableDictionaryFullName = $"{Namespace}.TrackableDictionary";
        public const string TrackOnlyAttributeFullName = $"{Namespace}.TrackOnlyAttribute";
        public const string NotTrackedAttributeFullName = $"{Namespace}.NotTrackedAttribute";
    }
}