# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-12-31

### 🚨 BREAKING CHANGES

#### Removed `ChangeTrackerFactory` Class
- **Impact**: HIGH - Direct API removal
- **What changed**: The `ChangeTrackerFactory.GetOrCreate<T>()` static method has been removed
- **Migration**:
  ```csharp
  // ❌ OLD (will not compile):
  var tracker = ChangeTrackerFactory.GetOrCreate(myEntity);
  var tracker = ChangeTrackerFactory.GetOrCreate(myEntity, useCache: false);

  // ✅ NEW:
  var tracker = myEntity.GetOrCreateChangeTracker(); // Auto-creates and caches
  var tracker = myEntity.TryGetChangeTracker();      // Returns null if not initialized
  ```

#### Removed `ITrackableBase<TEntity>` Interface
- **Impact**: MEDIUM
- **What changed**: The `ITrackableBase<TEntity>` interface no longer exists
- **Migration**:
  ```csharp
  // ❌ OLD:
  public void Process<T>(ITrackableBase<T> entity) where T : class { }
  public class MyEntity : ITrackableBase<MyEntity> { }

  // ✅ NEW:
  public void Process<T>(ITrackable<T> entity) where T : class { }
  public class MyEntity : ITrackable<MyEntity> { }
  ```

#### Changed `ITrackable<TEntity>` Interface Contract
- **Impact**: HIGH - Core API change
- **What changed**:
  - Removed: `IChangeTracker<TEntity> GetChangeTracker()`
  - Added: `IChangeTracker<TEntity>? TryGetChangeTracker()` (returns nullable)
  - Added: `IChangeTracker<TEntity> GetOrCreateChangeTracker()`
- **Migration**:
  ```csharp
  // If you were implementing ITrackable<T> manually:

  // ❌ OLD:
  public class Model : ITrackable<Model>
  {
      public IChangeTracker<Model> GetChangeTracker()
      {
          return ChangeTrackerFactory.GetOrCreate(this);
      }
  }

  ```

#### Renamed `IChangeTrackerBase` to `IChangeTracker`
- **Impact**: MEDIUM
- **What changed**: The non-generic base interface has been renamed


#### Changed `Extensions.GetChangeTracker()` Behavior
- **Impact**: MEDIUM
- **What changed**:
  - Parameter type changed from `this T model` to `this ITrackable<T> model`
  - Now throws `InvalidOperationException` if tracker is not initialized (previously auto-created)
- **Migration**:
  ```csharp
  // ❌ OLD (auto-created tracker silently):
  var tracker = myEntity.GetChangeTracker(); // Always worked

  // ✅ NEW Options:

  // Option 1: Use GetOrCreateChangeTracker() for auto-creation
  var tracker = myEntity.GetOrCreateChangeTracker();

  // Option 2: Use TryGetChangeTracker() to check if initialized
  var tracker = myEntity.TryGetChangeTracker();
  if (tracker != null)
  {
      // Use tracker
  }

  // Option 3: Initialize first with AsTrackable()
  myEntity.AsTrackable(); // Initializes tracker
  var tracker = myEntity.GetChangeTracker(); // Now safe to call

  // Option 4: Chain AsTrackable() and GetChangeTracker()
  var tracker = myEntity.AsTrackable().GetChangeTracker();
  ```

#### Changed Property Setter Pattern in Manual Implementation
- **Impact**: HIGH for manual implementations
- **What changed**: The recommended pattern for recording changes in setters has changed
- **Migration**:
  ```csharp
  // ❌ OLD (still works but deprecated):
  public string Name
  {
      get => field;
      set
      {
          this.GetChangeTracker().RecordChange(nameof(Name), field, value);
          field = value;
      }
  }

  // ✅ NEW (recommended - uses CallerMemberName):
  public string Name
  {
      get => field;
      set
      {
          this.GetOrCreateChangeTracker().RecordChange(field, value);
          // or
          this.SetField(ref field, value); // Helper extension method
          field = value;
      }
  }
  ```

#### Changed `DeepTracking<T>.SetTrackableProperties()` Signature
- **Impact**: MEDIUM
- **What changed**: Parameter type changed from `List<Func<T, IChangeTrackerBase?>>` to `List<Func<T, IChangeTracker?>>`
- **Migration**:
  ```csharp
  // ❌ OLD:
  static Model()
  {
      DeepTracking<Model>.SetTrackableProperties([
          x => x.SubModel?.GetChangeTracker(),
          x => x.Items as IChangeTrackerBase
      ]);
  }

  // ✅ NEW:
  static Model()
  {
      DeepTracking<Model>.SetTrackableProperties([
          x => x.SubModel?.TryGetChangeTracker(),    // Use TryGetChangeTracker()
          x => x.Items?.TryGetChangeTracker()        // Use TryGetChangeTracker()
      ]);
  }
  ```

#### Changed Collection Types Base Interface
- **Impact**: LOW
- **What changed**: `TrackableList<T>` and `TrackableDictionary<TKey, TValue>` now implement `IChangeTracker` instead of `IChangeTrackerBase`
- **Migration**:
  ```csharp
  // ❌ OLD:
  void ProcessCollection<T>(T collection) where T : IChangeTrackerBase { }

  // ✅ NEW:
  void ProcessCollection<T>(T collection) where T : IChangeTracker { }
  ```

### 🗑️ Deprecated (Not Breaking Yet)

#### Deprecated `RecordChange(string, TProperty, TProperty)` Overload
- **What changed**: The `RecordChange<TProperty>(string propertyName, TProperty currentValue, TProperty newValue)` method is marked with `[Obsolete]`
- **Why**: The parameter order was confusing, and `[CallerMemberName]` automatically captures the property name
- **Migration**:
  ```csharp
  // ⚠️ DEPRECATED (still works but shows warning):
  tracker.RecordChange(nameof(MyProp), oldValue, newValue);

  // ✅ PREFERRED:
  tracker.RecordChange(oldValue, newValue); // propertyName captured via [CallerMemberName]

  // Or use the SetField helper:
  this.SetField(ref field, value); // Automatically calls RecordChange
  ```

### ✨ Added

#### New Extension Methods
- `TryGetChangeTracker<T>(this ITrackable<T> model)` - Gets tracker without throwing if not initialized
- `GetOrCreateChangeTracker<T>(this T model)` - Gets or creates tracker (replaces ChangeTrackerFactory)
- `TryGetChangeTracker<T>(this List<T> model)` - Gets tracker from TrackableList if applicable
- `TryGetChangeTracker<TKey,TValue>(this Dictionary<TKey,TValue> model)` - Gets tracker from TrackableDictionary if applicable
- `SetField<TEntity, TField>(this ITrackable<TEntity> trackable, ref TField? field, TField value, [CallerMemberName] string? propertyName = null)` - Helper method for property setters with automatic change tracking
- `SetField<TEntity, TValue>(this ITrackable<TEntity> trackable, ref TrackableList<TValue>? field, List<TValue>? value, [CallerMemberName] string? propertyName = null)` - Helper for TrackableList properties
- `SetField<TEntity, TKey, TValue>(this ITrackable<TEntity> trackable, ref TrackableDictionary<TKey, TValue>? field, Dictionary<TKey, TValue>? value, [CallerMemberName] string? propertyName = null)` - Helper for TrackableDictionary properties

#### New Internal Methods
- `TrackingCache<T>.TryGet(T instance)` - Internal method to get tracker without creating it

#### Source Generator Improvements
- Updated source generator to emit `SetField()` helper calls in property setters for cleaner generated code
- Source generator now uses `TryGetChangeTracker()` in deep tracking setup for safer null handling
- Generated code uses `[CallerMemberName]` attribute to automatically capture property names

### 🐛 Fixed

- Fixed deep tracking to properly handle null trackers using `TryGetChangeTracker()?.IsDirty()` pattern
- Improved capitalization in exception messages for consistency
- Fixed `TrackableList<T>` and `TrackableDictionary<TKey,TValue>` to use safe null-conditional operator when checking `IsDirty()` on nested trackables

### ⚡ Performance

- Optimized `TrackableList<T>` internal operations
- Optimized `TrackableDictionary<TKey,TValue>` internal operations
- Reduced allocations by using `TryGetChangeTracker()` instead of auto-creating trackers unnecessarily

### 🔄 Changed (Non-Breaking)

#### Improved `AsTrackable<T>()` Implementation
- Now uses `GetOrCreateChangeTracker()?.Enable()` for safer null handling
- More defensive against edge cases

#### Better Internal Error Handling
- `RecordChange` now properly validates parameters before processing
- Improved exception messages for better developer experience

## Migration Guide Summary

### Quick Migration Steps

1. **Replace `ChangeTrackerFactory` calls**:
   ```csharp
   // Before: ChangeTrackerFactory.GetOrCreate(entity)
   // After:  entity.GetOrCreateChangeTracker()
   ```

2. **Update interface references**:
   ```csharp
   // Before: ITrackableBase<T> or IChangeTrackerBase
   // After:  ITrackable<T> or IChangeTracker
   ```

3. **Update deep tracking setup**:
   ```csharp
   // Before: x => x.SubModel?.GetChangeTracker()
   // After:  x => x.SubModel?.TryGetChangeTracker()
   ```

4. **Update manual property setters** (if not using source generator):
   ```csharp
   // Before: this.GetChangeTracker().RecordChange(nameof(Prop), field, value); field = value;
   // After:  this.SetField(ref field, value);
   ```

5. **Handle potentially uninitialized trackers**:
   ```csharp
   // Before: var tracker = entity.GetChangeTracker(); // Auto-created
   // After:  var tracker = entity.GetOrCreateChangeTracker(); // Explicit
   ```

### Testing Your Migration

After migrating, verify:
- [ ] All compilation errors are resolved
- [ ] Change tracking still works as expected
- [ ] Deep tracking functions correctly
- [ ] Collection tracking operates properly
- [ ] Rollback and AcceptChanges work
- [ ] No runtime exceptions from uninitialized trackers

### Recommended Upgrade Path

1. **Update NuGet packages** to the latest version
2. **Fix compilation errors** - Start with interface renames
3. **Replace ChangeTrackerFactory** calls with extension methods
4. **Update deep tracking** configuration in static constructors
5. **Consider using new SetField helpers** for cleaner property setters
6. **Run tests** to ensure change tracking behavior is preserved
7. **Address deprecation warnings** (optional but recommended)

## Version History

### [1.0.0] - 2025-12-31
This is the first stable release with significant API improvements and breaking changes. See above for full details.

### [0.5.5] - Previous Release
- See git history for previous changes

## Links
- [1.0.0]: https://github.com/erradil/Err.ChangeTracking/releases/tag/v1.0.0

---

## Need Help?

If you encounter issues during migration:
- 📖 Check the updated [README.md](docs/README.md) for examples
- 🐛 Report issues at [GitHub Issues](https://github.com/erradil/Err.ChangeTracking/issues)
- 💬 Ask questions in [GitHub Discussions](https://github.com/erradil/Err.ChangeTracking/discussions)