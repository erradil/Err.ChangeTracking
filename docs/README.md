# Err.ChangeTracking: Effortless Property Change Tracking for .NET

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.svg)](https://www.nuget.org/packages/Err.ChangeTracking)
[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.SourceGenerator.svg)](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator)

## Why Err.ChangeTracking?

Have you ever faced these challenges in your .NET applications?

- **No Entity Framework available**, but you still need to track object changes
- **Overhead of reflection-based change tracking** slowing down your application
- **Complex manual property change notification** code cluttering your models
- Need for **AOT compilation support** with Blazor, MAUI, or NativeAOT

**Err.ChangeTracking** leverages C# 13's revolutionary **partial properties** feature to provide zero-reflection change tracking with minimal runtime overhead and full AOT compatibility.

## Key Features

- **Track property changes** without manual PropertyChanged events
- **Collection tracking** for Lists and Dictionaries
- **Deep tracking** for nested objects and complex hierarchies
- **Rollback changes** to original values with a single method call
- **Selectively track properties** using attributes
- **Zero runtime reflection** for maximum performance
- **100% AOT compatible** for all modern .NET scenarios

## Requirements

- **.NET 9.0** or higher (recommended), OR
- **.NET 8.0** with `<LangVersion>preview</LangVersion>` in your project file

## Installation

```bash
dotnet add package Err.ChangeTracking
dotnet add package Err.ChangeTracking.SourceGenerator
```

---

## Quick Overview

### See It in Action

```csharp
using Err.ChangeTracking;

// 1. Create and initialize tracking
var order = new Order { Id = "ORD-001" }.AsTrackable();

// 2. Make changes
order.Id = "ORD-002";

// 3. Inspect changes
Console.WriteLine(order.GetChangeTracker().IsDirty);                    // True
Console.WriteLine(order.GetChangeTracker().HasChanged(x => x.Id));      // True
Console.WriteLine(order.GetChangeTracker().GetOriginalValue(x => x.Id)); // "ORD-001"

// 4. Rollback or accept
order.GetChangeTracker().Rollback();    // Reverts to "ORD-001"
// OR
order.GetChangeTracker().AcceptChanges(); // Accepts "ORD-002" as new baseline
```

### How to Track Changes

#### Option 1: Source Generator (Recommended) ⭐

Clean code with **C# 13 partial class & properties** (Important)- tracking code is generated separately!

```csharp
[Trackable]
public partial class Order
{
    public partial string Id { get; set; }  // ← Clean! No boilerplate

    [TrackCollection]
    public partial List<string> Tags { get; set; }

    [NotTracked]  // ← Exclude from tracking
    public partial DateTime CreatedDate { get; set; }
}
```

**What you write** vs **what gets generated**:

```csharp
// Your clean code (Order.cs)
[Trackable]
public partial class Order
{
    public partial string Id { get; set; }
}

// Auto-generated (Order.g.cs) - you never touch this!
public partial class Order : ITrackable<Order>
{
    private partial string _id;

    public partial string Id
    {
        get => _id;
        set => this.SetField(ref _id, value);  // Tracking magic happens here
    }
}
```

#### Option 2: Manual with C# 13 `field` Keyword

For fine-grained control with modern C#:

```csharp
public class Order : ITrackable<Order>
{
    public string Name
    {
        get => field;
        set => this.SetField(ref field!, value);  // ← Tracks changes
    }

    public int Quantity
    {
        get => field;
        set => this.SetField(ref field, value);
    }
}
```

#### Option 3: Manual Traditional

Classic approach with explicit backing fields:

```csharp
public class Order : ITrackable<Order>
{
    private string _name;
    public string Name
    {
        get => _name;
        set => this.SetField(ref _name, value);
    }
}
```
#### Option 4: Manual with Instance Storage (High Performance)
For performance-critical scenarios, opt-in to instance storage rather then the default cache:

```csharp
public class Order : ITrackable<Order>, IAttachedTracker<Order>
{
    // Explicit inline changeTracker property from interface implementation - hidden from public API
    IChangeTracker<Order>? IAttachedTracker<Order>.ChangeTracker { get; set; }

    public string Id
    {
        get => field;
        set => this.SetField(ref field!, value);
    }
}
```
### What Makes It Special?

| Feature                        | Benefit |
|--------------------------------|---------|
| ✨ **C# 13 Partial Properties** | Your code stays clean - tracking code lives in generated files |
| 🚀 **Zero Reflection**         | AOT-compatible, blazing fast performance |
| ⚡  **Instance Storage**        | faster tracker access (automatic with source generator) |
| 🔄 **Rollback/AcceptChanges**  | Built-in workflow like Entity Framework |
| 📦 **Collection Tracking**     | Lists and Dictionaries just work |
| 🔍 **Deep Tracking**           | Track changes in nested object hierarchies |
| 🎯 **Selective Tracking**      | Choose which properties to track with attributes |

---

## ⚡ Quick Start (3 Steps)

### Step 1: Initialize Tracking (REQUIRED!)

```csharp
// ✅ CORRECT - Initialize with .AsTrackable()
var order = new Order { Id = "ORD-001" }.AsTrackable();

// ❌ WRONG - Missing .AsTrackable()
var order2 = new Order { Id = "ORD-001" };  // Won't track changes!
```

**Important**: You **must** call `.AsTrackable()` to enable change tracking. Without it, changes won't be tracked and `GetChangeTracker()` will throw an exception.

### Step 2: Make Changes

```csharp
order.Id = "ORD-002";
order.Tags.Add("Urgent");
```

### Step 3: Use the Tracker

```csharp
var tracker = order.GetChangeTracker();

if (tracker.IsDirty)
{
    // Option A: Rollback all changes
    tracker.Rollback();
    Console.WriteLine(order.Id);  // Back to "ORD-001"

    // Option B: Accept and save
    await repository.SaveAsync(order);
    tracker.AcceptChanges();  // Establish new baseline
}
```

---

## 🤖 Source Generator (Recommended)

The source generator automates change tracking implementation by generating clean, efficient code at compile time. Thanks to **C# 13's class & partial properties**, all tracking code is generated in a separate file, keeping your original code pristine.

### How It Works

When you mark a class with `[Trackable]` and make class & properties `partial`, the source generator creates a companion partial class that implements the tracking logic.

**Your code stays clean:**

```csharp
// Order.cs - Your original file stays pristine
[Trackable]
public partial class Order                        // Must be partial
{
    public partial string Id { get; set; }        // Just declare it partial
    public partial decimal Total { get; set; }    // That's it!
}
```

**Generated code (Order.g.cs - automatically created):**

```csharp
/// Order.g.cs - Auto-generated, never edit this file
public partial class Order : ITrackable<Order>, IAttachedTracker<Order>
{
    // Instance storage for maximum performance
    IChangeTracker<Order>? IAttachedTracker<Order>.ChangeTracker { get; set; }

    private string _id;
    private decimal _total;

    public partial string Id
    {
        get => _id;
        set => this.SetField(ref _id, value);
    }

    public partial decimal Total
    {
        get => _total;
        set => this.SetField(ref _total, value);
    }
}
```
---

### 🎯 Attribute Reference

| Attribute | Target | Purpose | Example |
|-----------|--------|---------|---------|
| `[Trackable]` | Class | Enables change tracking for the class | `[Trackable]` |
| `[Trackable(Mode = TrackingMode.OnlyMarked)]` | Class | Only track properties with `[TrackOnly]` | `[Trackable(Mode = TrackingMode.OnlyMarked)]` |
| `[TrackOnly]` | Property | Include property in tracking (OnlyMarked mode) | `[TrackOnly] public partial string Name { get; set; }` |
| `[NotTracked]` | Property | Exclude property from tracking | `[NotTracked] public partial DateTime LastLogin { get; set; }` |
| `[TrackCollection]` | Property | Wrap collections in trackable wrappers | `[TrackCollection] public partial List<string> Tags { get; set; }` |
| `[DeepTracking]` | Property | Enable deep tracking for nested objects | `[DeepTracking] public partial Customer Customer { get; set; }` |

### Basic Usage

```csharp
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }    // Will be tracked
    public partial int Age { get; set; }        // Will be tracked
    public string? Notes { get; set; }          // Won't be tracked (not partial)
}

// Usage
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();
person.Name = "Bob";

Console.WriteLine(person.GetChangeTracker().IsDirty); // True
Console.WriteLine(person.GetChangeTracker().HasChanged(x => x.Name)); // True
```

### Selective Tracking with TrackingMode.OnlyMarked

Track only specific properties using `[TrackOnly]`:

```csharp
[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial class Document
{
    [TrackOnly]
    public partial string Title { get; set; }      // ✅ Tracked

    [TrackOnly]
    public partial string Content { get; set; }    // ✅ Tracked

    public partial DateTime LastViewed { get; set; } // ❌ NOT tracked
}

// Usage
var doc = new Document
{
    Title = "Doc1",
    Content = "Content",
    LastViewed = DateTime.Now
}.AsTrackable();

doc.Title = "New Title";       // ✅ Tracked
doc.LastViewed = DateTime.Now; // ❌ Not tracked

Console.WriteLine(doc.GetChangeTracker().HasChanged(x => x.Title));      // True
Console.WriteLine(doc.GetChangeTracker().HasChanged(x => x.LastViewed)); // False
```

### Excluding Properties with [NotTracked]

```csharp
[Trackable]
public partial class User
{
    public partial string Username { get; set; }    // ✅ Tracked

    [NotTracked]
    public partial DateTime LastLogin { get; set; } // ❌ NOT tracked

    public partial string Email { get; set; }       // ✅ Tracked
}
```

### Collection Tracking with [TrackCollection]

Automatically wrap collections in trackable wrappers:

```csharp
[Trackable]
public partial class Order
{
    public partial string Id { get; set; }

    [TrackCollection]
    public partial List<string> Tags { get; set; }  // → TrackableList<string>

    [TrackCollection]
    public partial Dictionary<string, decimal> Prices { get; set; }  // → TrackableDictionary<string, decimal>
}

// Usage
var order = new Order
{
    Id = "ORD001",
    Tags = new List<string> { "urgent" },
    Prices = new Dictionary<string, decimal> { ["item1"] = 10.99m }
}.AsTrackable();

order.Tags.Add("priority");        // Collection change tracked
order.Prices["item2"] = 15.99m;    // Dictionary change tracked

Console.WriteLine(order.Tags.AsTrackable().IsDirty);   // True
Console.WriteLine(order.Prices.AsTrackable().IsDirty); // True
```

### Deep Tracking with [DeepTracking]

Automatically configure deep tracking for nested objects:

```csharp
[Trackable]
public partial class Order
{
    public partial string Id { get; set; }

    [DeepTracking]
    public partial Customer Customer { get; set; }

    [DeepTracking, TrackCollection]
    public partial List<OrderItem> Items { get; set; }
}

[Trackable]
public partial class Customer
{
    public partial string Name { get; set; }
}

[Trackable]
public partial class OrderItem
{
    public partial string ProductName { get; set; }
    public partial int Quantity { get; set; }
}

// Usage - IMPORTANT: Make nested objects trackable!
var order = new Order
{
    Id = "ORD001",
    Customer = new Customer { Name = "John" }.AsTrackable(),  // ✅ Make trackable
    Items = new List<OrderItem>
    {
        new OrderItem { ProductName = "Widget", Quantity = 2 }.AsTrackable()  // ✅ Make trackable
    }
}.AsTrackable();

order.Customer.Name = "Jane";      // Deep change tracked
order.Items.AsTrackable()[0].Quantity = 3;       // Deep collection item change tracked

Console.WriteLine(order.GetChangeTracker().IsDirty(deepTracking: true)); // True
```

### Nested Records/Structs

```csharp
[Trackable]
public partial record Person
{
    public partial string Name { get; set; }

    [DeepTracking]
    public partial Address Addr { get; set; }

    [Trackable]
    public partial record Address
    {
        public partial string Street { get; set; }
        public partial string City { get; set; }
    }
}
```

---

## 🔧 Manual Implementation

For scenarios requiring fine-grained control, you can manually implement change tracking. This approach is perfect when you need custom validation, conditional tracking, or complex business logic in your setters.

### Simple Property Tracking

Implement the `ITrackable<T>` interface and add change tracking to your property setters:

```csharp
using Err.ChangeTracking;

public class Person : ITrackable<Person>
{
    private string _name;
    public string Name
    {
        get => _name;
        set => this.SetField(ref _name, value);
    }

    private int _age;
    public int Age
    {
        get => _age;
        set => this.SetField(ref _age, value);
    }
}

// Usage
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();
person.Name = "Bob";

Console.WriteLine(person.GetChangeTracker().IsDirty); // True
Console.WriteLine(person.GetChangeTracker().HasChanged(x => x.Name)); // True
```

### Simplified with C# 13 `field` Keyword (.NET 9)

With .NET 9 and C# 13, you can simplify property implementation using the `field` keyword, eliminating the need for explicit backing fields:

```csharp
public class Person : ITrackable<Person>
{
    public string Name
    {
        get => field;
        set => this.SetField(ref field!, value);
    }

    public int Age
    {
        get => field;
        set => this.SetField(ref field, value);
    }
}
```

**Note**: The `!` null-forgiving operator is used for reference types to satisfy the compiler.

### Collection Tracking

Track changes within collections by using `TrackableList<T>` or `TrackableDictionary<TKey, TValue>`:

```csharp
public class Order : ITrackable<Order>
{
    private TrackableList<string>? _items;

    public List<string>? Items
    {
        get => _items;
        set => this.SetField(ref _items, value);
    }
}

// Usage
var order = new Order { Items = new List<string> { "Item1" } }.AsTrackable();
order.Items.AsTrackable().Add("Item2"); // Collection change is tracked

Console.WriteLine(order.Items.AsTrackable().IsDirty); // True
```

### Deep Tracking for Nested Objects

Set up deep tracking for complex object hierarchies:

```csharp
public class Customer : ITrackable<Customer>
{
    private Address _address;
    private TrackableList<Order>? _orders;

    static Customer()
    {
        // Configure deep tracking for nested trackable objects
        DeepTracking<Customer>.SetTrackableProperties([
            x => x.Address?.TryGetChangeTracker(),    // Track nested Address changes
            x => x.Orders?.TryGetChangeTracker()      // Track Orders collection changes
        ]);
    }

    public Address Address
    {
        get => _address;
        set => this.SetField(ref _address, value);
    }

    public List<Order>? Orders
    {
        get => _orders;
        set => this.SetField(ref _orders, value);
    }
}

// Usage with deep tracking
var customer = new Customer
{
    Address = new Address { Street = "Main St" }.AsTrackable(),
    Orders = new List<Order>()
}.AsTrackable();

customer.Address.Street = "Oak St"; // Deep change is detected
Console.WriteLine(customer.GetChangeTracker().IsDirty(deepTracking: true)); // True
```

---

## 📚 Change Tracking Operations

### Checking for Changes

```csharp
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();
person.Name = "Bob";

// Check if any property changed
bool isDirty = person.GetChangeTracker().IsDirty;

// Check specific property using lambda
bool nameChanged = person.GetChangeTracker().HasChanged(x => x.Name);

// Check specific property using string
bool ageChanged = person.GetChangeTracker().HasChanged("Age");

// Get all changed property names
var changedProperties = person.GetChangeTracker().GetChangedProperties();
// Returns: ["Name"]

// Get original values using lambda
string originalName = person.GetChangeTracker().GetOriginalValue(x => x.Name);
// Returns: "Alice"

// Get all original values
var originalValues = person.GetChangeTracker().GetOriginalValues();
// Returns: Dictionary<string, object?> { ["Name"] = "Alice" }
```

### Rolling Back Changes

```csharp
// Rollback all changes
person.GetChangeTracker().Rollback();
Console.WriteLine(person.Name); // "Alice" - restored to original

// Rollback specific property using lambda
person.Name = "Charlie";
person.Age = 35;
person.GetChangeTracker().Rollback(x => x.Name);
Console.WriteLine(person.Name); // "Alice" - only Name rolled back
Console.WriteLine(person.Age);  // 35 - Age unchanged

// Rollback specific property using string
person.GetChangeTracker().Rollback("Age");
```

### Accepting Changes

```csharp
var order = new Order { Id = "ORD-001" }.AsTrackable();
order.Id = "ORD-002";

// Accept all changes (clear change history, establish new baseline)
order.GetChangeTracker().AcceptChanges();

Console.WriteLine(order.GetChangeTracker().IsDirty); // False
Console.WriteLine(order.Id); // "ORD-002"

// Make new changes
order.Id = "ORD-003";

// Original value is now the accepted value
Console.WriteLine(order.GetChangeTracker().GetOriginalValue(x => x.Id)); // "ORD-002" (not "ORD-001")

// Accept changes for specific property using lambda
order.GetChangeTracker().AcceptChanges(x => x.Id);

// Accept changes for specific property using string
order.GetChangeTracker().AcceptChanges("Id");
```

### Enabling and Disabling Tracking

```csharp
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();

// Disable tracking temporarily
person.GetChangeTracker().Enable(false);
person.Name = "Bob";      // NOT tracked
person.Age = 35;          // NOT tracked

// Re-enable tracking
person.GetChangeTracker().Enable(true);
person.Name = "Charlie";  // Tracked again

Console.WriteLine(person.GetChangeTracker().IsDirty); // True
Console.WriteLine(person.GetChangeTracker().HasChanged(x => x.Name)); // True (only "Charlie" change)
Console.WriteLine(person.GetChangeTracker().HasChanged(x => x.Age)); // False
```

---

### 🚀 Performance Benefits
- Zero reflection at runtime 
- all tracking code is generated at compile time
Minimal memory overhead 
- only stores original values for changed properties
AOT compatible 
- works with NativeAOT, Blazor, and MAUI
Efficient collections 
- trackable collections only monitor structural changes
Lazy evaluation 
- change trackers are created only when needed
Lambda expressions 
- strongly-typed property access without magic strings
Instance storage 
- 5-10x faster tracker access with direct field access

## ⚡ Instance attached tracker vs Cache-Based (default behavior)
Err.ChangeTracking supports two storage strategies for change trackers, each optimized for different scenarios:

### Cache-Based Storage (Default)
Simple and backward-compatible. The change tracker is stored in a global [ConditionalWeakTable](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2?view=net-10.0):
```csharp
// Simple - just implement ITrackable
public class Person : ITrackable<Person>
{
    public string Name
    {
        get => field;
        set => this.SetField(ref field!, value);
    }
}

// Usage
var person = new Person { Name = "Alice" }.AsTrackable();
var tracker = person.TryGetChangeTracker(); // Cache lookup
```
**Pros:**
- ✅ Zero boilerplate
- ✅ Works with any class
- ✅ Backward compatible

**Cons:**
- ⚠️ Bit slower than instance storage (~10-20ns per access)
- ⚠️ Shared cache lookup overhead
---
#### Instance attached (embeded) tracker (High Performance) 
#### Stores the change tracker directly in the object for maximum performance:

```csharp
// Manual implementation with instance storage
public class Order : ITrackable<Order>, IAttachedTracker<Order>
{
    // Empbedded change tracker in the instance
    // Explicit interface implementation - hidden from IntelliSense
    IChangeTracker<Order>? IAttachedTracker<Order>.ChangeTracker { get; set; }

    public string Id
    {
        get => field;
        set => this.SetField(ref field!, value);
    }
}

// Usage - same API, better performance!
var order = new Order { Id = "ORD-001" }.AsTrackable();
var tracker = order.TryGetChangeTracker(); // Direct field access - 5-10x faster!
```
**Pros:**

- ✅ 5-10x faster than cache (~2-3ns vs ~10-20ns)
- ✅ Direct field access, zero lookup overhead
- ✅ Same public API as cache-based
- ✅ Hidden from IntelliSense (explicit interface)

**Cons:**
- ⚠️ Requires one line of boilerplate (or use source generator)
---
#### Source Generator: Best of Both Worlds
The source generator automatically uses instance storage, giving you maximum performance with zero boilerplate:
```csharp
// Your code - clean and simple
[Trackable]
public partial class Product
{
    public partial string Name { get; set; }
}

// Generated code - automatically includes instance storage
public partial class Product : ITrackable<Product>, IAttachedTracker<Product>
{
    // Instance storage for maximum performance
    IChangeTracker<Product>? IAttachedTracker<Product>.ChangeTracker { get; set; }

    private string _name;
    public partial string Name
    {
        get => _name;
        set => this.SetField(ref _name, value);
    }
}
```

**You get:**

- ✅ Zero boilerplate
- ✅ Maximum performance (instance storage)
- ✅ Clean, readable code


### Auto-Generated Attached Tracker (Manual + Performance)

For manual implementations that need instance storage without boilerplate, simply make your class `partial`. The source generator will automatically add the inline `ChangeTracker` property:

```csharp
// Your code - just add partial keyword
public partial class Person : ITrackable<Person>
{
    public string Name
    {
        get => field;
        set => this.SetField(ref field!, value);
    }
}

// Auto-generated by AttachedTrackerGenerator
public partial class Person : IAttachedTracker<Person>
{
    IChangeTracker<Person>? IAttachedTracker<Person>.ChangeTracker { get; set; }
}
```

**When to use**: You want manual control over property setters (custom validation, logic) but still get instance storage benefits. Just add `partial` to your class - no `[Trackable]` attribute needed.


#### When to Use Instance Storage?

Use instance storage (IAttachedTracker<T>) when:
- ✅ You access the change tracker frequently in hot code paths
- ✅ You're tracking thousands of entities
- ✅ Performance benchmarks show cache lookup overhead
- ✅ Using the source generator (automatic!)

Stick with cache-based when:
- ✅ Simple scenarios with infrequent tracker access
- ✅ You prefer zero boilerplate over performance
- ✅ Backward compatibility is required

**Recommendation: Use the source generator - you get instance storage performance automatically with no extra effort!**

## 🏗️ Real-World Scenarios

### Scenario 1: Repository Pattern with Dapper (Dynamic SQL Updates)

Build UPDATE queries dynamically using only the changed fields, reducing database overhead and improving performance.

```csharp
using Dapper;
using System.Data;

public class PersonRepository
{
    private readonly IDbConnection _db;

    public PersonRepository(IDbConnection db)
    {
        _db = db;
    }

    // Load entity and initialize tracking
    public async Task<Person?> GetByIdAsync(int id)
    {
        var person = await _db.QuerySingleOrDefaultAsync<Person>(
            "SELECT * FROM Persons WHERE Id = @Id",
            new { Id = id }
        );

        return person?.AsTrackable();  // ✅ Initialize tracking before returning
    }

    // Load multiple entities
    public async Task<List<Person>> GetAllAsync()
    {
        var people = await _db.QueryAsync<Person>("SELECT * FROM Persons");

        // Initialize tracking for all entities
        return people.Select(p => p.AsTrackable()).ToList();
    }

    // Save only changed fields - dynamic UPDATE
    public async Task<bool> UpdateAsync(Person person)
    {
        var tracker = person.GetChangeTracker();

        if (!tracker.IsDirty)
            return false;  // Nothing to save

        // Build dynamic UPDATE with only changed fields
        var changedProps = tracker.GetChangedProperties();
        var setClause = string.Join(", ",
            changedProps.Select(p => $"{p} = @{p}")
        );

        var sql = $@"
            UPDATE Persons
            SET {setClause}, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        var parameters = new DynamicParameters();
        parameters.Add("Id", person.Id);
        parameters.Add("UpdatedAt", DateTime.UtcNow);

        // Add only changed properties
        foreach (var prop in changedProps)
        {
            var value = typeof(Person).GetProperty(prop)?.GetValue(person);
            parameters.Add(prop, value);
        }

        await _db.ExecuteAsync(sql, parameters);

        tracker.AcceptChanges();  // ✅ Mark as saved, establish new baseline
        return true;
    }

    // Batch update with automatic rollback on errors
    public async Task<BatchResult> SaveManyAsync(List<Person> people)
    {
        var result = new BatchResult();

        foreach (var person in people)
        {
            var tracker = person.GetChangeTracker();

            if (!tracker.IsDirty)
            {
                result.SkippedCount++;
                continue;  // Skip unchanged entities
            }

            try
            {
                await UpdateAsync(person);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                tracker.Rollback();  // ✅ Undo changes on error
                result.ErrorCount++;
                result.Errors.Add($"Person {person.Id}: {ex.Message}");
            }
        }

        return result;
    }

    // Insert new entity
    public async Task<int> InsertAsync(Person person)
    {
        var sql = @"
            INSERT INTO Persons (Name, Email, Age, CreatedAt, UpdatedAt)
            VALUES (@Name, @Email, @Age, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var id = await _db.ExecuteScalarAsync<int>(sql, new
        {
            person.Name,
            person.Email,
            person.Age,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        person.Id = id;

        // Initialize tracking after insert
        person.AsTrackable();
        person.GetChangeTracker().AcceptChanges();  // ✅ Clean state

        return id;
    }
}

public class BatchResult
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
```

**Example Usage:**

```csharp
var repository = new PersonRepository(dbConnection);

// Load and modify
var person = await repository.GetByIdAsync(123);
person.Name = "Updated Name";
person.Email = "new@email.com";

// Only Name and Email are updated in the database!
await repository.UpdateAsync(person);

// Batch update with error handling
var people = await repository.GetAllAsync();
foreach (var p in people)
{
    p.Age += 1;  // Birthday!
}

var result = await repository.SaveManyAsync(people);
Console.WriteLine($"Saved: {result.SuccessCount}, Errors: {result.ErrorCount}");
```

### Scenario 2: Domain Events - Property-Specific Triggers

Publish integration events when specific properties change, enabling event-driven architectures and microservices communication.

```csharp
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : class;
}

// Domain events
public record OrderStatusChangedEvent(
    string OrderId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt
);

public record OrderPriorityChangedEvent(
    string OrderId,
    string NewPriority
);

public record OrderReassignedEvent(
    string OrderId,
    int FromCustomerId,
    int ToCustomerId
);

public record OrderUpdatedEvent(
    string OrderId,
    List<string> ChangedProperties
);

public class OrderEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderEventHandler> _logger;

    public OrderEventHandler(IEventBus eventBus, ILogger<OrderEventHandler> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task PublishChangesAsync(Order order)
    {
        var tracker = order.GetChangeTracker();

        if (!tracker.IsDirty)
        {
            _logger.LogDebug("Order {OrderId} has no changes", order.Id);
            return;
        }

        // Trigger specific event when Status changes
        if (tracker.HasChanged(x => x.Status))
        {
            var oldStatus = tracker.GetOriginalValue(x => x.Status);
            var newStatus = order.Status;

            _logger.LogInformation(
                "Order {OrderId} status changed from {OldStatus} to {NewStatus}",
                order.Id, oldStatus, newStatus
            );

            await _eventBus.PublishAsync(new OrderStatusChangedEvent(
                order.Id,
                oldStatus,
                newStatus,
                DateTime.UtcNow
            ));

            // Trigger additional actions based on status transitions
            if (newStatus == "Completed" && oldStatus != "Completed")
            {
                // Send completion notification
                await _eventBus.PublishAsync(new OrderCompletedEvent(order.Id));
            }
        }

        // Different event for Priority changes
        if (tracker.HasChanged(x => x.Priority))
        {
            var newPriority = order.Priority;

            _logger.LogInformation(
                "Order {OrderId} priority changed to {Priority}",
                order.Id, newPriority
            );

            await _eventBus.PublishAsync(new OrderPriorityChangedEvent(
                order.Id,
                newPriority
            ));

            // High priority orders trigger alerts
            if (newPriority == "High" || newPriority == "Critical")
            {
                await _eventBus.PublishAsync(new HighPriorityOrderEvent(order.Id));
            }
        }

        // Trigger integration event for Customer assignment
        if (tracker.HasChanged(x => x.CustomerId))
        {
            var oldCustomerId = tracker.GetOriginalValue(x => x.CustomerId);
            var newCustomerId = order.CustomerId;

            _logger.LogInformation(
                "Order {OrderId} reassigned from customer {OldCustomerId} to {NewCustomerId}",
                order.Id, oldCustomerId, newCustomerId
            );

            await _eventBus.PublishAsync(new OrderReassignedEvent(
                order.Id,
                oldCustomerId,
                newCustomerId
            ));
        }

        // Composite event when multiple important properties change
        if (tracker.HasChanged(x => x.Status) ||
            tracker.HasChanged(x => x.Priority) ||
            tracker.HasChanged(x => x.CustomerId))
        {
            var changedProps = tracker.GetChangedProperties();

            await _eventBus.PublishAsync(new OrderUpdatedEvent(
                order.Id,
                changedProps
            ));
        }

        // Trigger event for Total amount changes (fraud detection)
        if (tracker.HasChanged(x => x.Total))
        {
            var oldTotal = tracker.GetOriginalValue(x => x.Total);
            var newTotal = order.Total;

            // Large changes trigger fraud check
            if (Math.Abs(newTotal - oldTotal) > 1000m)
            {
                await _eventBus.PublishAsync(new LargeTotalChangeEvent(
                    order.Id, oldTotal, newTotal
                ));
            }
        }
    }

    // Advanced: Publish events for each changed property
    public async Task PublishDetailedChangesAsync<T>(T entity, string entityId)
        where T : ITrackable<T>
    {
        var tracker = entity.GetChangeTracker();

        foreach (var propertyName in tracker.GetChangedProperties())
        {
            var oldValue = tracker.GetOriginalValue<object>(propertyName);
            var newValue = typeof(T).GetProperty(propertyName)?.GetValue(entity);

            await _eventBus.PublishAsync(new PropertyChangedEvent(
                EntityType: typeof(T).Name,
                EntityId: entityId,
                PropertyName: propertyName,
                OldValue: oldValue,
                NewValue: newValue,
                ChangedAt: DateTime.UtcNow
            ));
        }
    }
}

public record PropertyChangedEvent(
    string EntityType,
    string EntityId,
    string PropertyName,
    object? OldValue,
    object? NewValue,
    DateTime ChangedAt
);
```

**Example Usage:**

```csharp
// In your service or command handler
public class OrderService
{
    private readonly OrderRepository _repository;
    private readonly OrderEventHandler _eventHandler;

    public async Task UpdateOrderAsync(string orderId, UpdateOrderCommand command)
    {
        // Load trackable entity
        var order = await _repository.GetByIdAsync(orderId);

        // Make changes
        order.Status = command.Status;
        order.Priority = command.Priority;
        order.CustomerId = command.CustomerId;

        // Save changes
        await _repository.UpdateAsync(order);

        // Publish domain events based on what changed
        await _eventHandler.PublishChangesAsync(order);

        // Accept changes after successful publish
        order.GetChangeTracker().AcceptChanges();
    }
}
```

### Scenario 3: Audit Trail Generation

Automatically generate comprehensive audit logs from tracked changes.

```csharp
public class AuditEntry
{
    public int Id { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string PropertyName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AuditService
{
    private readonly IAuditRepository _auditRepository;

    public async Task<List<AuditEntry>> GenerateAuditTrailAsync<T>(
        T entity,
        string entityId,
        string userId)
        where T : ITrackable<T>
    {
        var tracker = entity.GetChangeTracker();
        var auditEntries = new List<AuditEntry>();

        foreach (var propertyName in tracker.GetChangedProperties())
        {
            var oldValue = tracker.GetOriginalValue<object>(propertyName);
            var newValue = GetPropertyValue(entity, propertyName);

            auditEntries.Add(new AuditEntry
            {
                EntityType = typeof(T).Name,
                EntityId = entityId,
                PropertyName = propertyName,
                OldValue = SerializeValue(oldValue),
                NewValue = SerializeValue(newValue),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow
            });
        }

        if (auditEntries.Any())
        {
            await _auditRepository.SaveAsync(auditEntries);
        }

        return auditEntries;
    }

    private object? GetPropertyValue<T>(T entity, string propertyName)
    {
        return typeof(T).GetProperty(propertyName)?.GetValue(entity);
    }

    private string? SerializeValue(object? value)
    {
        if (value == null) return null;

        // Handle collections
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            return System.Text.Json.JsonSerializer.Serialize(enumerable);
        }

        return value.ToString();
    }
}
```

### Scenario 5: Collection Tracking - Deep Changes

Track changes to individual items within collections.

```csharp
[Trackable]
public partial class ShoppingCart
{
    public partial string Id { get; set; }

    [TrackCollection, DeepTracking]
    public partial List<CartItem> Items { get; set; }
}

[Trackable]
public partial class CartItem
{
    public partial string ProductId { get; set; }
    public partial int Quantity { get; set; }
    public partial decimal Price { get; set; }
}

// Usage
var cart = new ShoppingCart
{
    Id = "CART-001",
    Items = new List<CartItem>
    {
        new CartItem { ProductId = "P1", Quantity = 1, Price = 10m }.AsTrackable(),
        new CartItem { ProductId = "P2", Quantity = 2, Price = 20m }.AsTrackable()
    }
}.AsTrackable();

// Modify item in collection
cart.Items[0].Quantity = 5;

// Check individual item tracking
var item1Tracker = cart.Items[0].GetChangeTracker();
Console.WriteLine(item1Tracker.IsDirty); // True
Console.WriteLine(item1Tracker.GetOriginalValue(x => x.Quantity)); // 1

// Add new item
cart.Items.AsTrackable().Add(
    new CartItem { ProductId = "P3", Quantity = 1, Price = 30m }.AsTrackable()
);

// Collection is dirty
Console.WriteLine(cart.Items.AsTrackable().IsDirty); // True

// Save changes
await cartRepository.SaveAsync(cart);

// Accept changes for all items
cart.GetChangeTracker().AcceptChanges();
foreach (var item in cart.Items)
{
    item.GetChangeTracker().AcceptChanges();
}
```

### Scenario 6: AcceptChanges Workflow - Multi-Step Process

Establish baselines at different stages of a workflow.

```csharp
public class OrderWorkflowService
{
    private readonly OrderRepository _repository;
    private readonly IValidator<Order> _validator;

    public async Task<WorkflowResult> ProcessOrderAsync(string orderId)
    {
        // Step 1: Load order
        var order = await _repository.GetByIdAsync(orderId);
        var tracker = order.GetChangeTracker();

        // Step 2: Draft stage - user makes changes
        order.Status = "Draft";
        order.Total = CalculateTotal(order);

        // Step 3: Validation
        var validationResult = await _validator.ValidateAsync(order);
        if (!validationResult.IsValid)
        {
            tracker.Rollback();  // ✅ Undo draft changes
            return WorkflowResult.ValidationFailed(validationResult.Errors);
        }

        // Step 4: Save draft
        await _repository.UpdateAsync(order);
        tracker.AcceptChanges();  // ✅ Draft is now the baseline

        // Step 5: User continues editing
        order.Priority = "High";
        order.Notes = "Urgent order";

        // Step 6: Submit for approval
        order.Status = "Pending Approval";

        // Step 7: Save again
        await _repository.UpdateAsync(order);
        tracker.AcceptChanges();  // ✅ New baseline

        // Original values are now from the last AcceptChanges
        // Not from the initial load!

        return WorkflowResult.Success();
    }
}
```

---

## 🔑 Essential Concepts

### What is `.AsTrackable()`?

`.AsTrackable()` is an extension method that **initializes change tracking** for your entity. It creates the change tracker and enables tracking in a single call.

**What it does:**
1. Creates or retrieves the change tracker for the entity
2. Calls `.Enable()` internally to activate tracking
3. Returns the same entity instance (fluent API)

**Usage:**

```csharp
// ✅ CORRECT
var order = new Order { Id = "ORD-001" }.AsTrackable();

// ❌ WRONG - Missing .AsTrackable()
var order2 = new Order { Id = "ORD-001" };
order2.GetChangeTracker().IsDirty;  // ❌ Throws InvalidOperationException!
```

### When to Call `.AsTrackable()`

Call `.AsTrackable()` in these scenarios:

**Pattern 1: Inline (recommended)**
```csharp
var entity = new Person { Name = "Alice" }.AsTrackable();
```

**Pattern 2: Repository pattern**
```csharp
public async Task<Person> GetByIdAsync(int id)
{
    var person = await _db.QuerySingleAsync<Person>("SELECT * FROM Persons WHERE Id = @id", new { id });
    return person.AsTrackable();
}
```


For more advanced initialization patterns, see the [Advanced Initialization Patterns](#-advanced-initialization-patterns) section.

---

## 🔄 Migration Guide

### From Manual Implementation to Source Generator

**Before (Manual):**

```csharp
public class Person : ITrackable<Person>
{
    private string _name;
    public string Name
    {
        get => _name;
        set => this.SetField(ref _name, value);
    }
}
```

**After (Source Generator):**

```csharp
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }
}
```

### From INotifyPropertyChanged

**Before:**

```csharp
public class Person : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

**After:**

```csharp
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }
}
```

---

## ❓ Frequently Asked Questions

### Q: Can I use this with Entity Framework?

A: Absolutely! Err.ChangeTracking complements Entity Framework's change tracking. You can use it in your business layer, for DTOs, ViewModels, or when you need more granular control over change detection.

### Q: Does it work with records?

A: Yes! `record class` is fully supported with the source generator, but `record struct` is not supported because it is a value type.

### Q: What about performance compared to manual implementation?

A: The generated code is nearly identical to hand-written manual implementation, with zero runtime overhead.

### Q: Can I track changes in inherited properties?

A: Yes, inheritance is fully supported. Mark the base class as `[Trackable]` and derived classes will inherit change tracking capabilities.

### Q: Is it thread-safe?

A: The change tracker itself is not thread-safe by design (for performance). If you need thread-safety, implement appropriate locking mechanisms in your application.

### Q: Can I disable tracking temporarily?

A: Yes, use `GetChangeTracker().Enable(false)` to temporarily disable tracking.

---

## 🐛 Troubleshooting

### Common Issues and Solutions

#### Issue: "ChangeTracker is not initialized!" exception

This is the **#1 most common issue**. It happens when you forget to call `.AsTrackable()`:

```csharp
// ❌ PROBLEM
var person = new Person { Name = "Alice" };
person.GetChangeTracker().IsDirty; // Throws: ChangeTracker is not initialized!

// ✅ SOLUTION
var person = new Person { Name = "Alice" }.AsTrackable();
person.GetChangeTracker().IsDirty; // Works!
```

**Fix:**
- Always call `.AsTrackable()` after creating or loading entities
- Use `TryGetChangeTracker()` to check if tracking is initialized without throwing

#### Issue: Properties not being tracked

- Ensure the class is marked with `[Trackable]`
- Ensure properties are declared as `partial`
- Check that the property has a setter
- Verify the property isn't marked with `[NotTracked]`
- **Most importantly: Ensure you called `.AsTrackable()` on the entity**

#### Issue: Deep tracking not working

- Ensure nested objects implement `ITrackable<T>` or are marked with `[Trackable]`
- Add `[DeepTracking]` attribute to properties that should be deeply tracked
- Call `IsDirty(deepTracking: true)` to check deep changes
- **Make sure nested objects are initialized with `.AsTrackable()`**

#### Issue: Collection changes not detected

- Use `[TrackCollection]` attribute on collection properties
- Ensure you're using the property (not the backing field) when modifying collections
- Call `.AsTrackable()` on the root entity
- For collection internal changes, use `collection.AsTrackable().IsDirty`

#### Issue: Source generator not running

- Ensure you have both packages installed: `Err.ChangeTracking` and `Err.ChangeTracking.SourceGenerator`
- Clean and rebuild your solution
- Check that your project targets .NET 8+ with C# 13 language version
- Restart Visual Studio / your IDE

#### Issue: Collections throw "not TrackableList" error

```csharp
// ❌ PROBLEM
var regularList = new List<string>();
regularList.AsTrackable();  // Throws: ArgumentException

// ✅ SOLUTION
[TrackCollection]
public partial List<string> Tags { get; set; }  // Generated as TrackableList

// OR manually
var trackableList = new TrackableList<string>();
```

---

## 📝 License

Licensed under the [MIT License](LICENSE).

---

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

---

## 📞 Support

- 🐛 **Issues**: [GitHub Issues](https://github.com/erradil/Err.ChangeTracking/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/erradil/Err.ChangeTracking/discussions)

---

> Built with ❤️ for the .NET community by **ERRADIL**