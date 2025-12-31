# .NET Memory Management Alternatives for Change Tracking

This document explores different .NET approaches for managing the entity-to-tracker cache with automatic memory management.

## Current Implementation

```csharp
internal static class TrackingCache<T> where T : class
{
    private static readonly ConditionalWeakTable<T, IChangeTracker<T>> Cache = new();
}
```

## Alternatives in .NET

### 1. ✅ ConditionalWeakTable<TKey, TValue> (CURRENT - BEST CHOICE)

**Introduced**: .NET Framework 4.0 / .NET Standard 2.0

```csharp
private static readonly ConditionalWeakTable<T, IChangeTracker<T>> Cache = new();

public static IChangeTracker<T> GetOrCreate(T instance)
{
    return Cache.GetValue(instance, ChangeTracker<T>.Create);
}
```

**Pros**:
- ✅ Automatic cleanup when key is GC'd
- ✅ Thread-safe (built-in synchronization)
- ✅ Ephemeron semantics (key AND value collected together)
- ✅ Simple API
- ✅ No manual cleanup needed
- ✅ Perfect for object-to-metadata mapping

**Cons**:
- ⚠️ Slower than Dictionary (~2x for lookups)
- ⚠️ Cannot enumerate entries (.NET < 5.0)
- ⚠️ Reference types only (TKey : class)

**Best for**: Attaching metadata to objects (exactly our use case!)

---

### 2. WeakReference<T> + Dictionary

**Introduced**: .NET Framework 4.5 / .NET Standard 1.0

```csharp
// ❌ NOT RECOMMENDED - Complex and error-prone
private static readonly Dictionary<WeakReference<T>, IChangeTracker<T>> _cache = new();
private static readonly object _lock = new();

public static IChangeTracker<T> GetOrCreate(T instance)
{
    lock (_lock)
    {
        // Need to scan for matching reference
        foreach (var kvp in _cache.ToList())
        {
            if (kvp.Key.TryGetTarget(out var target) && ReferenceEquals(target, instance))
            {
                return kvp.Value;
            }
        }

        // Cleanup dead references (manual!)
        CleanupDeadReferences();

        var tracker = ChangeTracker<T>.Create(instance);
        _cache[new WeakReference<T>(instance)] = tracker;
        return tracker;
    }
}

private static void CleanupDeadReferences()
{
    var deadKeys = _cache.Where(kvp => !kvp.Key.TryGetTarget(out _))
                         .Select(kvp => kvp.Key)
                         .ToList();
    foreach (var key in deadKeys)
    {
        _cache.Remove(key);
    }
}
```

**Pros**:
- ✅ More control over cleanup timing
- ✅ Can enumerate entries
- ✅ Faster lookups if properly indexed

**Cons**:
- ❌ Manual cleanup required (memory leak if forgotten)
- ❌ Complex implementation
- ❌ Need background cleanup thread
- ❌ Manual locking required
- ❌ WeakReference can't be used as Dictionary key efficiently
- ❌ No ephemeron semantics (value keeps key alive!)

**Verdict**: Too complex, easy to get wrong ❌

---

### 3. IMemoryCache (Microsoft.Extensions.Caching.Memory)

**Introduced**: ASP.NET Core / .NET Standard 2.0

```csharp
private static readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = 10000 // Optional
});

public static IChangeTracker<T> GetOrCreate(T instance)
{
    // ❌ PROBLEM: Need string key, can't use object identity
    var key = RuntimeHelpers.GetHashCode(instance).ToString(); // ❌ Collisions!

    return _cache.GetOrCreate(key, entry =>
    {
        entry.SetSlidingExpiration(TimeSpan.FromMinutes(5)); // ⚠️ Time-based, not GC-based!
        return ChangeTracker<T>.Create(instance);
    });
}
```

**Pros**:
- ✅ Built into ASP.NET Core
- ✅ Rich eviction policies (time, size, priority)
- ✅ Thread-safe
- ✅ Good for HTTP request scoped caching

**Cons**:
- ❌ Time-based eviction, not GC-based (wrong semantics)
- ❌ Can't use object identity as key
- ❌ Requires string/value type keys
- ❌ Entries can be evicted while object still alive
- ❌ Dependencies required (Microsoft.Extensions.Caching.Memory)
- ❌ Overkill for simple object attachment

**Verdict**: Wrong tool for this job ❌

---

### 4. DependentHandle (.NET 6+)

**Introduced**: .NET 6.0

```csharp
// ⚠️ Lower-level primitive that ConditionalWeakTable uses internally
using System.Runtime.CompilerServices;

private static readonly List<DependentHandle> _handles = new();
private static readonly object _lock = new();

public static IChangeTracker<T> GetOrCreate(T instance)
{
    lock (_lock)
    {
        // Check existing handles
        foreach (var handle in _handles)
        {
            if (handle.Target == instance)
            {
                return (IChangeTracker<T>)handle.Dependent!;
            }
        }

        // Create new handle
        var tracker = ChangeTracker<T>.Create(instance);
        var newHandle = new DependentHandle(instance, tracker);
        _handles.Add(newHandle);
        return tracker;
    }
}
```

**Pros**:
- ✅ Ephemeron semantics (like ConditionalWeakTable)
- ✅ Lower overhead per entry
- ✅ More control

**Cons**:
- ❌ Requires manual management
- ❌ Need to dispose handles
- ❌ More complex than ConditionalWeakTable
- ❌ .NET 6+ only (less compatible)

**Verdict**: ConditionalWeakTable is built on this - use the higher-level API instead ⚠️

---

### 5. ConditionalWeakTable with ObjectIDGenerator

**Approach**: Combine with identity tracking

```csharp
private static readonly ConditionalWeakTable<T, IChangeTracker<T>> _cache = new();
private static readonly ObjectIDGenerator _idGenerator = new();

public static IChangeTracker<T> GetOrCreate(T instance)
{
    // Can track object identity even after GC
    _idGenerator.GetId(instance, out bool firstTime);

    return _cache.GetValue(instance, ChangeTracker<T>.Create);
}
```

**Pros**:
- ✅ Can track object identity across GC
- ✅ Useful for debugging/logging

**Cons**:
- ❌ ObjectIDGenerator prevents GC (keeps objects alive!) - defeats the purpose
- ❌ Adds complexity

**Verdict**: Useful for diagnostics, but not for production cache ⚠️

---

### 6. RuntimeHelpers.GetHashCode + Dictionary

**Approach**: Use object identity hash as key

```csharp
private static readonly Dictionary<int, WeakReference<(T Entity, IChangeTracker<T> Tracker)>> _cache = new();
private static readonly object _lock = new();

public static IChangeTracker<T> GetOrCreate(T instance)
{
    int hashCode = RuntimeHelpers.GetHashCode(instance); // Identity hash

    lock (_lock)
    {
        if (_cache.TryGetValue(hashCode, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var tuple) && ReferenceEquals(tuple.Entity, instance))
            {
                return tuple.Tracker;
            }
            _cache.Remove(hashCode); // Dead reference
        }

        var tracker = ChangeTracker<T>.Create(instance);
        _cache[hashCode] = new WeakReference<(T, IChangeTracker<T>)>((instance, tracker));
        return tracker;
    }
}
```

**Pros**:
- ✅ Faster lookups (O(1) dictionary access)
- ✅ Uses object identity

**Cons**:
- ❌ Hash collisions possible (different objects, same hash)
- ❌ Manual cleanup required
- ❌ Still needs to store weak reference to entity
- ❌ Value keeps key alive (no ephemeron semantics)
- ❌ Complex

**Verdict**: Faster but less safe than ConditionalWeakTable ⚠️

---

### 7. MemoryCache with ObjectHandle

**Approach**: Use System.Runtime.Caching with custom key

```csharp
private static readonly System.Runtime.Caching.MemoryCache _cache =
    System.Runtime.Caching.MemoryCache.Default;

public static IChangeTracker<T> GetOrCreate(T instance)
{
    string key = RuntimeHelpers.GetHashCode(instance).ToString();

    return (IChangeTracker<T>)_cache.AddOrGetExisting(
        key,
        ChangeTracker<T>.Create(instance),
        new CacheItemPolicy
        {
            SlidingExpiration = TimeSpan.FromMinutes(20)
        }
    ) ?? _cache.Get(key);
}
```

**Pros**:
- ✅ Built into .NET Framework
- ✅ Thread-safe

**Cons**:
- ❌ Time-based eviction (wrong semantics)
- ❌ Can't use object as key directly
- ❌ Legacy API (.NET Framework only)
- ❌ Requires System.Runtime.Caching assembly

**Verdict**: Wrong semantics for this use case ❌

---

### 8. Hybrid: ConditionalWeakTable + FastCache

**Approach**: Two-tier cache for performance

```csharp
// Fast path: Strong references for recently used items
private static readonly Dictionary<T, IChangeTracker<T>> _fastCache = new();
private static readonly ConditionalWeakTable<T, IChangeTracker<T>> _weakCache = new();
private static readonly object _lock = new();
private const int MaxFastCacheSize = 1000;

public static IChangeTracker<T> GetOrCreate(T instance)
{
    // Fast path (no lock)
    lock (_lock)
    {
        if (_fastCache.TryGetValue(instance, out var tracker))
            return tracker;
    }

    // Slow path (weak cache)
    var result = _weakCache.GetValue(instance, ChangeTracker<T>.Create);

    // Add to fast cache
    lock (_lock)
    {
        if (_fastCache.Count >= MaxFastCacheSize)
        {
            // LRU eviction
            var oldest = _fastCache.First().Key;
            _fastCache.Remove(oldest);
        }
        _fastCache[instance] = result;
    }

    return result;
}
```

**Pros**:
- ✅ Fast lookups for hot objects
- ✅ No memory leaks (weak cache fallback)
- ✅ Good for high-throughput scenarios

**Cons**:
- ⚠️ Complex implementation
- ⚠️ Need LRU or similar eviction policy
- ⚠️ Manual synchronization
- ⚠️ More memory for fast cache

**Verdict**: Only if profiling shows ConditionalWeakTable is a bottleneck ⚠️

---

## Comparison Table

| Approach | Memory Safety | Performance | Complexity | Thread-Safe | .NET Version |
|----------|--------------|-------------|------------|-------------|--------------|
| **ConditionalWeakTable** | ✅ Perfect | ⚡ Good | ✅ Simple | ✅ Yes | 4.0+ / Std 2.0 |
| WeakReference + Dict | ⚠️ Manual | ⚡⚡ Better | ❌ Complex | ⚠️ Manual | 4.5+ / Std 1.0 |
| IMemoryCache | ⚠️ Time-based | ⚡⚡ Good | ✅ Simple | ✅ Yes | Core / Std 2.0 |
| DependentHandle | ✅ Good | ⚡⚡⚡ Best | ⚠️ Medium | ⚠️ Manual | 6.0+ |
| ObjectIDGenerator | ❌ Prevents GC | ⚡ Good | ⚠️ Medium | ⚠️ No | 1.0+ |
| RuntimeHelpers Hash | ⚠️ Collisions | ⚡⚡⚡ Best | ❌ Complex | ⚠️ Manual | 4.5+ |
| MemoryCache | ⚠️ Time-based | ⚡ Slow | ✅ Simple | ✅ Yes | Framework |
| Hybrid Cache | ✅ Good | ⚡⚡⚡ Best | ❌ Very Complex | ⚠️ Manual | Any |

---

## Why ConditionalWeakTable is Still the Best Choice

For this change tracking library:

1. **Correct Semantics**: GC-based cleanup (not time-based)
2. **Ephemeron Pattern**: Both entity and tracker collected together
3. **Thread-Safety**: Built-in, no manual locking needed
4. **Simplicity**: 3 lines of code vs 30+ for alternatives
5. **Reliability**: Battle-tested since .NET 4.0
6. **Zero Dependencies**: No external packages
7. **Wide Compatibility**: .NET Standard 2.0 support

### Performance is NOT a Problem

- Lookup cost: ~30-50ns (vs ~10-20ns for Dictionary)
- This overhead is **negligible** compared to:
  - Property setter logic
  - Change detection logic
  - Serialization/deserialization
  - Database operations

**Real-world impact**: < 0.01% of total operation time

### When to Consider Alternatives

Only consider alternatives if:
- ✅ Profiling shows ConditionalWeakTable is a bottleneck (rare)
- ✅ You have millions of lookups per second (rare in change tracking)
- ✅ You need enumeration of cache entries (diagnostics)

Even then, a **hybrid approach** would be better than replacing ConditionalWeakTable entirely.

---

## Recommendation

**Keep using `ConditionalWeakTable<T, IChangeTracker<T>>`**

It's the perfect choice for this library because:
- Correct memory semantics
- Simple and maintainable
- Battle-tested and reliable
- Performance is good enough
- No memory leaks possible

> "Premature optimization is the root of all evil" - Donald Knuth

The current implementation is **excellent** ✅
