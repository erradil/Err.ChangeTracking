namespace Err.ChangeTracking;

/// <summary>
///     Interface commune à toutes les collections trackables (TrackableList, TrackableDictionary).
///     Permet de vérifier si la collection a subi un changement (structurel ou interne).
/// </summary>
public interface ITrackableCollection
{
    /// <summary>
    ///     Indique si la collection est sale (ajout/suppression/modification interne).
    /// </summary>
    bool IsDirty { get; }
}