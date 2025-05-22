namespace Err.ChangeTracking.SourceGenerator;

public static class Constants
{
    public const string Namespace = "Err.ChangeTracking";

    public static class Types
    {
        public const string TrackableAttributeFullName = $"{Namespace}.TrackableAttribute";
        public const string TrackOnlyAttributeFullName = $"{Namespace}.TrackOnlyAttribute";
        public const string NotTrackedAttributeFullName = $"{Namespace}.NotTrackedAttribute";
        public const string TrackCollectionAttributeFullName = $"{Namespace}.TrackCollectionAttribute";

        public const string ITrackableFullName = $"{Namespace}.ITrackable<T>";
        public const string DeepChangeTrackingFullName = $"{Namespace}.DeepChangeTracking<T>";

        public const string TrackableListFullName = $"{Namespace}.TrackableList<T>";
        public const string TrackableDictionaryFullName = $"{Namespace}.TrackableDictionary<TKey, TValue>";
    }
}