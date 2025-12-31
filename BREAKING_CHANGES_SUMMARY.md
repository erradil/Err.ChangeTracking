# Breaking Changes Summary - Err.ChangeTracking v1.0.0

## Executive Summary

This document provides a comprehensive overview of all breaking changes introduced in the upcoming major version release. These changes modernize the API, improve type safety, and provide better control over change tracker initialization.

## 🎯 Breaking Changes Overview

| # | Change | Impact | Migration Effort |
|---|--------|--------|------------------|
| 1 | Removed `ChangeTrackerFactory` class | HIGH | Low - Simple find/replace |
| 2 | Removed `ITrackableBase<T>` interface | MEDIUM | Low - Rename to `ITrackable<T>` |
| 3 | Changed `ITrackable<T>` interface contract | HIGH | Medium - API usage changes |
| 4 | Renamed `IChangeTrackerBase` to `IChangeTracker` | MEDIUM | Low - Simple rename |
| 5 | Changed `Extensions.GetChangeTracker()` behavior | MEDIUM | Medium - May expose bugs |
| 6 | Changed `DeepTracking<T>` signature | MEDIUM | Low - Update lambdas |
| 7 | Changed collection base interfaces | LOW | Low - Rare usage pattern |

## 📊 Detailed Analysis

### 1. Removed `ChangeTrackerFactory` Class

**Files Affected**: Any code calling `ChangeTrackerFactory.GetOrCreate()`

**Reason**: Simplified API surface, better encapsulation, extension methods are more discoverable

**Before**:
```csharp
var tracker = ChangeTrackerFactory.GetOrCreate(myEntity);
var tracker = ChangeTrackerFactory.GetOrCreate(myEntity, useCache: false);
```

**After**:
```csharp
var tracker = myEntity.GetOrCreateChangeTracker();  // Cached by default
var tracker = myEntity.TryGetChangeTracker();       // Returns null if not initialized
```

**Search Pattern**: `ChangeTrackerFactory.GetOrCreate`

---

### 2. Removed `ITrackableBase<TEntity>` Interface

**Files Affected**:
- Type constraints using `ITrackableBase<T>`
- Classes implementing `ITrackableBase<T>`
- Documentation examples (docs/README.md:122)

**Reason**: Simplified type hierarchy, `ITrackable<T>` now has default implementations

**Before**:
```csharp
public class OptimizedPerson : ITrackableBase<OptimizedPerson>
{
    public IChangeTracker<OptimizedPerson> GetChangeTracker()
    {
        return _changeTracker ??= ChangeTrackerFactory.GetOrCreate(this);
    }
}
```

**After**:
```csharp
public class OptimizedPerson : ITrackable<OptimizedPerson>
{
    // Default implementation provided by interface - no need to implement
    // Or optionally implement for custom caching behavior
}
```

**Search Pattern**: `ITrackableBase`

---

### 3. Changed `ITrackable<TEntity>` Interface Contract

**Files Affected**: Any manual implementations of `ITrackable<T>`

**What Changed**:
- ❌ Removed: `IChangeTracker<TEntity> GetChangeTracker()`
- ✅ Added: `IChangeTracker<TEntity>? TryGetChangeTracker()`
- ✅ Added: `IChangeTracker<TEntity> GetOrCreateChangeTracker()`

**Reason**:
- Better control over tracker initialization
- Null-safety improvements
- Clearer API semantics (explicit create vs get)

**Before**:
```csharp
public interface ITrackable<TEntity> : ITrackableBase<TEntity>
{
    IChangeTracker<TEntity> GetChangeTracker()
    {
        return ChangeTrackerFactory.GetOrCreate((TEntity)this);
    }
}
```

**After**:
```csharp
public interface ITrackable<TEntity>
{
    IChangeTracker<TEntity>? TryGetChangeTracker()
        => TrackingCache<TEntity>.TryGet((TEntity)this);

    IChangeTracker<TEntity> GetOrCreateChangeTracker()
        => TrackingCache<TEntity>.GetOrCreate((TEntity)this);
}
```

**Impact**: Classes implementing `ITrackable<T>` can now rely on default implementation

---

### 4. Renamed `IChangeTrackerBase` to `IChangeTracker`

**Files Affected**:
- Type constraints
- Variable declarations
- Deep tracking lambdas (docs/README.md:186, 461)

**Reason**: Simpler naming, removed unnecessary "Base" suffix

**Before**:
```csharp
IChangeTrackerBase tracker = myList;
x => x.Orders as IChangeTrackerBase
```

**After**:
```csharp
IChangeTracker tracker = myList;
x => x.Orders?.TryGetChangeTracker()
```

**Search Pattern**: `IChangeTrackerBase` or `IBaseTracker`

---

### 5. Changed `Extensions.GetChangeTracker()` Behavior

**Files Affected**: Any code calling `.GetChangeTracker()` without initialization

**What Changed**:
- Old: Auto-created tracker silently
- New: Throws `InvalidOperationException` if not initialized

**Reason**:
- Explicit is better than implicit
- Prevents accidental tracker creation
- Better performance (no hidden allocations)

**Before** (always worked):
```csharp
var entity = new MyEntity();
var tracker = entity.GetChangeTracker(); // Auto-created tracker
```

**After** (throws if not initialized):
```csharp
var entity = new MyEntity();

// Option 1: Explicit creation
var tracker = entity.GetOrCreateChangeTracker();

// Option 2: Check before use
var tracker = entity.TryGetChangeTracker();
if (tracker != null) { /* use tracker */ }

// Option 3: Initialize with AsTrackable()
entity.AsTrackable();
var tracker = entity.GetChangeTracker(); // Safe

// Option 4: Chain methods
var tracker = entity.AsTrackable().GetChangeTracker();
```

**Risk**: May expose latent bugs where code assumed tracker was initialized

---

### 6. Changed Property Setter Pattern

**Files Affected**: Manual implementations with property setters (not source-generated)

**What Changed**: Recommended pattern for setters

**Before**:
```csharp
public string Name
{
    get => field;
    set
    {
        this.GetChangeTracker().RecordChange(nameof(Name), field, value);
        field = value;
    }
}
```

**After** (modern pattern):
```csharp
public string Name
{
    get => field;
    set => this.SetField(ref field, value); // Simplest

    // Or:
    set
    {
        this.GetOrCreateChangeTracker().RecordChange(field, value);
        field = value;
    }
}
```

**Deprecation**: `RecordChange(string, T, T)` is marked `[Obsolete]`

---

### 7. Changed `DeepTracking<T>.SetTrackableProperties()` Signature

**Files Affected**: Static constructors setting up deep tracking

**What Changed**:
- Old: `List<Func<T, IChangeTrackerBase?>>`
- New: `List<Func<T, IChangeTracker?>>`

**Before**:
```csharp
static Customer()
{
    DeepTracking<Customer>.SetTrackableProperties([
        x => x.Address?.GetChangeTracker(),
        x => x.Orders as IChangeTrackerBase
    ]);
}
```

**After**:
```csharp
static Customer()
{
    DeepTracking<Customer>.SetTrackableProperties([
        x => x.Address?.TryGetChangeTracker(),
        x => x.Orders?.TryGetChangeTracker()
    ]);
}
```

**Search Pattern**: `DeepTracking<.*>.SetTrackableProperties`

---

## 📝 Documentation Updates Required

### Files Needing Updates:

1. **docs/README.md**
   - Line 62, 73: Update property setter examples
   - Lines 100-115: Update C# 13 field keyword examples
   - Lines 122-142: **CRITICAL** - Remove entire `ITrackableBase` section (outdated)
   - Lines 158-162: Update collection tracking setter
   - Lines 185-186: Update deep tracking lambda examples
   - Lines 195, 205: Update property setters
   - Lines 272-286: Update generated code example
   - Lines 460-462: Update deep tracking lambdas
   - Lines 752: Update validation example setter

2. **Samples/Examples**
   - samples/Err.ChangeTracking.SampleDemo/Models/ManualTracked.cs (already updated ✅)

3. **Tests** (for reference - already updated ✅)
   - tests/Err.ChangeTracking.Tests/TrackingTests.cs
   - tests/Err.ChangeTracking.Tests/DeepTrackingTests.cs

---

## 🔍 Code Search Queries for Migration

Use these patterns to find code needing updates:

```bash
# Find ChangeTrackerFactory usage
grep -r "ChangeTrackerFactory" --include="*.cs"

# Find ITrackableBase usage
grep -r "ITrackableBase" --include="*.cs"

# Find IChangeTrackerBase usage
grep -r "IChangeTrackerBase\|IBaseTracker" --include="*.cs"

# Find old RecordChange pattern
grep -r "RecordChange(nameof(" --include="*.cs"

# Find old DeepTracking pattern
grep -r "GetChangeTracker()" | grep "DeepTracking"

# Find GetChangeTracker() calls (review each)
grep -r "\.GetChangeTracker()" --include="*.cs"
```

---

## ✅ Migration Checklist

- [ ] Update NuGet package references to v1.0.0
- [ ] Run code search queries to identify affected files
- [ ] Replace `ChangeTrackerFactory.GetOrCreate()` → `GetOrCreateChangeTracker()`
- [ ] Replace `ITrackableBase<T>` → `ITrackable<T>`
- [ ] Replace `IChangeTrackerBase` → `IChangeTracker`
- [ ] Update deep tracking lambdas: `GetChangeTracker()` → `TryGetChangeTracker()`
- [ ] Review all `GetChangeTracker()` calls for initialization
- [ ] Consider using new `SetField()` helpers in property setters
- [ ] Update documentation and code comments
- [ ] Run full test suite
- [ ] Address compiler warnings about obsolete methods
- [ ] Update README examples (see list above)

---

## 🎁 New Features (Non-Breaking)

### New Helper Methods

```csharp
// Simplified property setters with automatic change tracking
this.SetField(ref field, value);
this.SetField(ref listField, newList);        // For TrackableList
this.SetField(ref dictField, newDict);        // For TrackableDictionary

// Better null safety
var tracker = entity.TryGetChangeTracker();   // Returns null instead of throwing
var tracker = list.TryGetChangeTracker();     // Works with List<T>
var tracker = dict.TryGetChangeTracker();     // Works with Dictionary<K,V>
```

---

## 📞 Support

If you encounter issues during migration:
- 📖 See [CHANGELOG.md](CHANGELOG.md) for detailed migration guide
- 📖 See updated [README.md](docs/README.md) for API examples
- 🐛 Report issues: https://github.com/erradil/Err.ChangeTracking/issues
- 💬 Ask questions: https://github.com/erradil/Err.ChangeTracking/discussions

---

## Summary Statistics

- **Breaking Changes**: 7
- **Deprecated APIs**: 1
- **New Features**: 8 extension methods
- **Documentation Updates**: ~12 sections
- **Estimated Migration Time**: 1-4 hours (depending on codebase size)
- **Risk Level**: MEDIUM (most changes are mechanical renames)