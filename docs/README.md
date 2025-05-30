# Err.ChangeTracking: Effortless Property Change Tracking for .NET

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.svg)](https://www.nuget.org/packages/Err.ChangeTracking)
[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.SourceGenerator.svg)](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator)

## 🚀 Why Err.ChangeTracking?

Have you ever faced these challenges in your .NET applications?

- **No Entity Framework available**, but you still need to track object changes
- **Overhead of reflection-based change tracking** slowing down your application
- **Complex manual property change notification** code cluttering your models
- Need for **AOT compilation support** with Blazor, MAUI, or NativeAOT

**Err.ChangeTracking** leverages C# 13's revolutionary **partial properties** feature to provide zero-reflection change
tracking with minimal runtime overhead and full AOT compatibility.

## ✨ Key Features

- **Track property changes** without manual PropertyChanged events
- **Collection tracking** for Lists and Dictionaries
- **Deep tracking** for nested objects and complex hierarchies
- **Rollback changes** to original values with a single method call
- **Selectively track properties** using attributes
- **Zero runtime reflection** for maximum performance
- **100% AOT compatible** for all modern .NET scenarios

## 📋 Requirements

- **.NET 9.0** or higher (recommended), OR
- **.NET 8.0** with `<LangVersion>preview</LangVersion>` in your project file

## 📦 Installation

```textmate
dotnet add package Err.ChangeTracking
dotnet add package Err.ChangeTracking.SourceGenerator
```

---

## 🔧 Manual Implementation

For scenarios requiring fine-grained control, you can manually implement change tracking. This approach is perfect when
you need custom validation, conditional tracking, or complex business logic in your setters.

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
        set 
        { 
            this.GetChangeTracker().RecordChange(_name, value); 
            _name = value; 
        }
    }

    private int _age;
    public int Age
    {
        get => _age;
        set 
        { 
            this.GetChangeTracker().RecordChange(_age, value); 
            _age = value; 
        }
    }
}

// Usage
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();
person.Name = "Bob";

Console.WriteLine(person.GetChangeTracker().IsDirty); // True
Console.WriteLine(person.GetChangeTracker().HasChanged(x => x.Name)); // True
```

### Simplified with C# 13 `field` Keyword (.NET 9)

With .NET 9 and C# 13, you can simplify property implementation using the `field` keyword, eliminating the need for
explicit backing fields:

```csharp
public class Person : ITrackable<Person>
{
    public string Name
    {
        get => field;
        set 
        { 
            this.GetChangeTracker().RecordChange(field, value); 
            field = value; 
        }
    }

    public int Age
    {
        get => field;
        set 
        { 
            this.GetChangeTracker().RecordChange(field, value); 
            field = value; 
        }
    }
}
```

### Advanced Manual Implementation with Private Cache

For performance optimization, you can cache the change tracker in a private field:

```csharp
public class OptimizedPerson : ITrackableBase<OptimizedPerson>
{
    private IChangeTracker<OptimizedPerson>? _changeTracker;

    public IChangeTracker<OptimizedPerson> GetChangeTracker()
    {
        return _changeTracker ??= ChangeTrackerFactory.GetOrCreate(this);
    }

    private string _name;
    public string Name
    {
        get => _name;
        set 
        { 
            _changeTracker?.RecordChange(_name, value); 
            _name = value; 
        }
    }
}
```

### Collection Tracking

Track changes within collections by using `TrackableList<T>` or `TrackableDictionary<TKey, TValue>`:

```csharp
public class Order : ITrackable<Order>
{
    private TrackableList<string>? _items;

    public List<string>? Items
    {
        get => _items;
        set
        {
            this.GetChangeTracker().RecordChange(_items, value);
            _items = value is null ? null : new TrackableList<string>(value);
        }
    }
}

// Usage
var order = new Order { Items = new List<string> { "Item1" } }.AsTrackable();
order.Items.Add("Item2"); // Collection change is tracked

Console.WriteLine(order.GetChangeTracker().IsDirty); // True
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
            x => x.Address?.GetChangeTracker(),  // Track nested Address changes
            x => x.Orders as IBaseTracker        // Track Orders collection changes
        ]);
    }

    public Address Address
    {
        get => _address;
        set 
        { 
            this.GetChangeTracker().RecordChange(_address, value); 
            _address = value; 
        }
    }

    public List<Order>? Orders
    {
        get => _orders;
        set
        {
            this.GetChangeTracker().RecordChange(_orders, value);
            _orders = value is null ? null : new TrackableList<Order>(value);
        }
    }
}

// Usage with deep tracking
var customer = new Customer 
{ 
    Address = new Address { Street = "Main St" },
    Orders = new List<Order>()
}.AsTrackable();

customer.Address.Street = "Oak St"; // Deep change is detected
Console.WriteLine(customer.GetChangeTracker().IsDirty(deepTracking: true)); // True
```

---

## 🤖 Source Generator (Recommended)

The source generator automates the manual implementation, generating clean, efficient code at compile time. Thanks to *
*C# 13's revolutionary partial properties feature**, we can implement change tracking in a separate partial class
without polluting your original code.

**The beauty of this approach:**

- Your original class stays clean and focused on business logic
- All change tracking code is generated in a separate partial class file
- You only need to make your class `partial` and add the `[Trackable]` attribute
- Make properties `partial` that you want to track
- Zero impact on your existing codebase structure

### How It Works

When you mark a class with `[Trackable]` and make properties `partial`, the source generator creates a companion partial
class that implements:

- The `ITrackable<T>` interface
- Backing fields for your properties
- Property implementations with change tracking logic
- Static constructors for deep tracking (when needed)

**Your code stays clean:**
```csharp
// YourBusinessModel.cs - Your original file stays pristine
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }    // Just declare it partial
    public partial int Age { get; set; }        // That's it!
}
```

**Generated code (automatically created in Person.g.cs):**
```csharp
// Person.g.cs - Auto-generated, never edit this file
public partial class Person : ITrackable<Person>
{
    private string _name;
    private int _age;
    
    public string Name
    {
        get => _name;
        set 
        { 
            this.GetChangeTracker().RecordChange(_name, value); 
            _name = value; 
        }
    }
    
    public int Age
    {
        get => _age;
        set 
        { 
            this.GetChangeTracker().RecordChange(_age, value); 
            _age = value; 
        }
    }
}
```

### Basic Usage

Mark your class with `[Trackable]` and make properties `partial`:

```csharp
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }    // Will be tracked
    public partial int Age { get; set; }        // Will be tracked
    public string? Notes { get; set; }          // Won't be tracked (not partial)
}

// Usage (same as manual)
var person = new Person { Name = "Alice", Age = 30 }.AsTrackable();
person.Name = "Bob";
Console.WriteLine(person.GetChangeTracker().IsDirty); // True
```

### Selective Tracking with TrackingMode.OnlyMarked

Track only specific properties using `[TrackOnly]`:

```csharp
[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial class Document
{
    [TrackOnly]
    public partial string Title { get; set; }      // Will be tracked
    
    [TrackOnly] 
    public partial string Content { get; set; }    // Will be tracked
    
    public partial DateTime LastViewed { get; set; } // Won't be tracked
}

// Usage
var doc = new Document 
{ 
    Title = "Doc1", 
    Content = "Content", 
    LastViewed = DateTime.Now 
}.AsTrackable();

doc.Title = "New Title";     // Tracked
doc.LastViewed = DateTime.Now; // Not tracked

Console.WriteLine(doc.GetChangeTracker().HasChanged(x => x.Title)); // True
Console.WriteLine(doc.GetChangeTracker().HasChanged(x => x.LastViewed)); // False
```

### Excluding Properties with [NotTracked]

Exclude specific properties from tracking:

```csharp
[Trackable]
public partial class User
{
    public partial string Username { get; set; }    // Will be tracked
    
    [NotTracked]
    public partial DateTime LastLogin { get; set; } // Won't be tracked
    
    public partial string Email { get; set; }       // Will be tracked
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
    public partial List<string> Tags { get; set; }              // → TrackableList<string>
    
    [TrackCollection]
    public partial Dictionary<string, decimal> Prices { get; set; } // → TrackableDictionary<string, decimal>
}

// Usage
var order = new Order 
{ 
    Id = "ORD001", 
    Tags = new List<string> { "urgent" },
    Prices = new Dictionary<string, decimal> { ["item1"] = 10.99m }
}.AsTrackable();

order.Tags.Add("priority");           // Collection change tracked
order.Prices["item2"] = 15.99m;       // Dictionary change tracked

Console.WriteLine(order.GetChangeTracker().IsDirty); // True
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
    
    [DeepTracking]
    [TrackCollection]
    public partial List<OrderItem> Items { get; set; }
}

[Trackable]
public partial class Customer
{
    public partial string Name { get; set; }
    public partial string Email { get; set; }
}

[Trackable]
public partial class OrderItem
{
    public partial string ProductName { get; set; }
    public partial int Quantity { get; set; }
}

// Usage
var order = new Order
{
    Id = "ORD001",
    Customer = new Customer { Name = "John", Email = "john@example.com" },
    Items = new List<OrderItem> 
    { 
        new OrderItem { ProductName = "Widget", Quantity = 2 }
    }
}.AsTrackable();

order.Customer.Name = "Jane";         // Deep change tracked
order.Items[0].Quantity = 3;          // Deep collection item change tracked

Console.WriteLine(order.GetChangeTracker().IsDirty(deepTracking: true)); // True
```

### Generated Code Structure

The source generator creates clean, maintainable code that follows these patterns:

```csharp
// What you write:
[Trackable]
public partial class Product
{
    [DeepTracking]
    public partial Category Category { get; set; }
    
    [TrackCollection, DeepTracking]
    public partial List<string> Tags { get; set; }
}

// What gets generated:
public partial class Product : ITrackable<Product>
{
    static Product()
    {
        // Auto-generated deep tracking configuration
        DeepTracking<Product>.SetTrackableProperties([
            x => x.Category?.GetChangeTracker(),
            x => x.Tags as IBaseTracker
        ]);
    }
    
    private Category _category;
    private TrackableList<string>? _tags;
    
    public Category Category
    {
        get => _category;
        set 
        { 
            this.GetChangeTracker().RecordChange(_category, value); 
            _category = value; 
        }
    }
    
    public List<string>? Tags
    {
        get => _tags;
        set
        {
            this.GetChangeTracker().RecordChange(_tags, value);
            _tags = value is null ? null : new TrackableList<string>(value);
        }
    }
}
```

This approach keeps your business models clean while providing powerful change tracking capabilities through
compile-time code generation.

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

// Get all changed property names
var changedProperties = person.GetChangeTracker().GetChangedProperties();

// Get original values using lambda
var originalValues = person.GetChangeTracker().GetOriginalValues();
string originalName = person.GetChangeTracker().GetOriginalValue(x => x.Name);
```

### Rolling Back Changes

```csharp
// Rollback all changes
person.GetChangeTracker().Rollback();

// Rollback specific property using lambda
person.GetChangeTracker().Rollback(x => x.Name);

// Rollback specific property using string (also available)
person.GetChangeTracker().Rollback("Name");
```

### Accepting Changes

```csharp
// Accept all changes (clear change history)
person.GetChangeTracker().AcceptChanges();

// Accept changes for specific property using lambda
person.GetChangeTracker().AcceptChanges(x => x.Name);

// Accept changes for specific property using string (also available)
person.GetChangeTracker().AcceptChanges("Name");
```

---

## 🏗️ Real-World Scenarios

### 1. Smart Repository Pattern

Optimize database operations by updating only changed properties:

```csharp
public class PersonRepository
{
    private readonly IDbConnection _db;

    public async Task<bool> SaveAsync(Person person)
    {
        var tracker = person.GetChangeTracker();
        
        if (!tracker.IsDirty)
            return false; // No changes to save
        
        var changedProperties = tracker.GetChangedProperties();
        var setClause = string.Join(", ", changedProperties.Select(p => $"{p} = @{p}"));
        var sql = $"UPDATE Persons SET {setClause} WHERE Id = @Id";
        
        await _db.ExecuteAsync(sql, person);
        tracker.AcceptChanges(); // Mark as saved
        
        return true;
    }
}
```

### 2. Form Validation and Dirty Detection

Track user changes in forms for better UX:

```csharp
// In a Blazor component
@code {
    private UserProfile _profile = new UserProfile().AsTrackable();
    
    private bool HasUnsavedChanges => _profile.GetChangeTracker().IsDirty;
    
    private async Task SaveChanges()
    {
        if (!HasUnsavedChanges)
        {
            await ShowMessage("No changes to save");
            return;
        }
        
        var changedFields = _profile.GetChangeTracker().GetChangedProperties();
        await ValidateFields(changedFields);
        
        await _userService.UpdateProfileAsync(_profile);
        _profile.GetChangeTracker().AcceptChanges();
        
        await ShowMessage("Profile updated successfully");
    }
    
    private void DiscardChanges()
    {
        if (HasUnsavedChanges)
        {
            _profile.GetChangeTracker().Rollback();
            StateHasChanged();
        }
    }
}
```

### 3. Domain Events Based on Changes

Publish events when specific properties change:

```csharp
public class OrderService
{
    private readonly IEventPublisher _eventPublisher;
    
    public async Task UpdateOrderAsync(Order order)
    {
        var tracker = order.GetChangeTracker();
        
        // Save changes
        await _repository.SaveAsync(order);
        
        // Publish events based on what changed using lambda expressions
        if (tracker.HasChanged(x => x.Status))
        {
            var originalStatus = tracker.GetOriginalValue(x => x.Status);
            await _eventPublisher.PublishAsync(new OrderStatusChangedEvent(
                order.Id, originalStatus, order.Status
            ));
        }
        
        if (tracker.HasChanged(x => x.Priority))
        {
            await _eventPublisher.PublishAsync(new OrderPriorityChangedEvent(
                order.Id, order.Priority
            ));
        }
        
        tracker.AcceptChanges();
    }
}
```

### 4. Audit Trail Generation

Automatically generate audit logs from tracked changes:

```csharp
public class AuditService
{
    public async Task<List<AuditEntry>> GenerateAuditTrailAsync<T>(T entity, string userId) 
        where T : ITrackable<T>
    {
        var tracker = entity.GetChangeTracker();
        var auditEntries = new List<AuditEntry>();
        
        foreach (var propertyName in tracker.GetChangedProperties())
        {
            var originalValue = tracker.GetOriginalValue<object>(propertyName);
            var currentValue = GetCurrentValue(entity, propertyName);
            
            auditEntries.Add(new AuditEntry
            {
                EntityType = typeof(T).Name,
                PropertyName = propertyName,
                OldValue = originalValue?.ToString(),
                NewValue = currentValue?.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow
            });
        }
        
        return auditEntries;
    }
}
```

### 5. Undo/Redo Functionality

Implement undo/redo using change tracking:

```csharp
public class UndoRedoManager<T> where T : ITrackable<T>
{
    private readonly Stack<Dictionary<string, object?>> _undoStack = new();
    private readonly Stack<Dictionary<string, object?>> _redoStack = new();
    
    public void SaveState(T entity)
    {
        var tracker = entity.GetChangeTracker();
        if (tracker.IsDirty)
        {
            _undoStack.Push(new Dictionary<string, object?>(tracker.GetOriginalValues()));
            _redoStack.Clear(); // Clear redo stack when new changes are made
            tracker.AcceptChanges();
        }
    }
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    
    public void Undo(T entity)
    {
        if (!CanUndo) return;
        
        var currentState = CaptureCurrentState(entity);
        var previousState = _undoStack.Pop();
        
        _redoStack.Push(currentState);
        RestoreState(entity, previousState);
    }
    
    public void Redo(T entity)
    {
        if (!CanRedo) return;
        
        var currentState = CaptureCurrentState(entity);
        var nextState = _redoStack.Pop();
        
        _undoStack.Push(currentState);
        RestoreState(entity, nextState);
    }
}
```

### 6. Property Change Validation

Validate changes before they're applied:

```csharp
public class ValidatedEntity : ITrackable<ValidatedEntity>
{
    private string _email;
    
    public string Email
    {
        get => _email;
        set
        {
            // Validate before tracking the change
            if (!IsValidEmail(value))
                throw new ArgumentException("Invalid email format", nameof(value));
                
            this.GetChangeTracker().RecordChange(_email, value);
            _email = value;
        }
    }
    
    private static bool IsValidEmail(string email) => 
        !string.IsNullOrWhiteSpace(email) && email.Contains("@");
}
```

### 7. Conditional Change Tracking

Track changes only under certain conditions:

```csharp
public class ConditionalEntity : ITrackable<ConditionalEntity>
{
    private string _sensitiveData;
    private bool _trackingEnabled = true;
    
    public string SensitiveData
    {
        get => _sensitiveData;
        set
        {
            // Only track changes when tracking is enabled
            if (_trackingEnabled)
            {
                this.GetChangeTracker().RecordChange(_sensitiveData, value);
            }
            _sensitiveData = value;
        }
    }
    
    public void EnableTracking() => _trackingEnabled = true;
    public void DisableTracking() => _trackingEnabled = false;
}
```

### 8. Batch Operations with Change Detection

Process multiple entities efficiently:

```csharp
public class BatchProcessor
{
    public async Task<BatchResult> ProcessEntitiesAsync<T>(IEnumerable<T> entities) 
        where T : ITrackable<T>
    {
        var result = new BatchResult();
        
        foreach (var entity in entities)
        {
            var tracker = entity.GetChangeTracker();
            
            if (!tracker.IsDirty)
            {
                result.SkippedCount++;
                continue;
            }
            
            try
            {
                await ProcessEntityAsync(entity);
                tracker.AcceptChanges();
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                // Rollback on error
                tracker.Rollback();
                result.ErrorCount++;
                result.Errors.Add($"Entity failed: {ex.Message}");
            }
        }
        
        return result;
    }
}
```

### 9. Real-time Change Notifications

Implement real-time updates using SignalR:

```csharp
public class RealtimeEntityService<T> where T : ITrackable<T>
{
    private readonly IHubContext<EntityHub> _hubContext;
    
    public async Task BroadcastChangesAsync(T entity, string groupName)
    {
        var tracker = entity.GetChangeTracker();
        
        if (!tracker.IsDirty) return;
        
        var changes = new
        {
            EntityType = typeof(T).Name,
            Changes = tracker.GetChangedProperties().ToDictionary(
                prop => prop,
                prop => new
                {
                    OldValue = tracker.GetOriginalValue<object>(prop),
                    NewValue = GetCurrentValue(entity, prop)
                }
            )
        };
        
        await _hubContext.Clients.Group(groupName).SendAsync("EntityChanged", changes);
        tracker.AcceptChanges();
    }
}
```

### 10. Configuration Management

Track configuration changes with automatic persistence:

```csharp
[Trackable]
public partial class AppConfiguration
{
    public partial string DatabaseConnectionString { get; set; }
    public partial int MaxRetryAttempts { get; set; }
    public partial TimeSpan RequestTimeout { get; set; }
    
    [NotTracked]
    public DateTime LastModified { get; set; }
}

public class ConfigurationManager
{
    private readonly AppConfiguration _config;
    private readonly IConfigurationPersistence _persistence;
    
    public ConfigurationManager(IConfigurationPersistence persistence)
    {
        _persistence = persistence;
        _config = _persistence.Load().AsTrackable();
    }
    
    public async Task<bool> SaveChangesAsync()
    {
        var tracker = _config.GetChangeTracker();
        
        if (!tracker.IsDirty)
            return false;
        
        _config.LastModified = DateTime.UtcNow;
        await _persistence.SaveAsync(_config);
        tracker.AcceptChanges();
        
        return true;
    }
    
    public void RevertChanges()
    {
        _config.GetChangeTracker().Rollback();
    }
}
```

---

## 🎯 Attribute Reference

| Attribute                                     | Target   | Purpose                                        | Example                                                            |
|-----------------------------------------------|----------|------------------------------------------------|--------------------------------------------------------------------|
| `[Trackable]`                                 | Class    | Enables change tracking for the class          | `[Trackable]`                                                      |
| `[Trackable(Mode = TrackingMode.OnlyMarked)]` | Class    | Only track properties with `[TrackOnly]`       | `[Trackable(Mode = TrackingMode.OnlyMarked)]`                      |
| `[TrackOnly]`                                 | Property | Include property in tracking (OnlyMarked mode) | `[TrackOnly] public partial string Name { get; set; }`             |
| `[NotTracked]`                                | Property | Exclude property from tracking                 | `[NotTracked] public partial DateTime LastLogin { get; set; }`     |
| `[TrackCollection]`                           | Property | Wrap collections in trackable wrappers         | `[TrackCollection] public partial List<string> Tags { get; set; }` |
| `[DeepTracking]`                              | Property | Enable deep tracking for nested objects        | `[DeepTracking] public partial Customer Customer { get; set; }`    |

---

## 🚀 Performance Benefits

- **Zero reflection** at runtime - all tracking code is generated at compile time
- **Minimal memory overhead** - only stores original values for changed properties
- **AOT compatible** - works with NativeAOT, Blazor, and MAUI
- **Efficient collections** - trackable collections only monitor structural changes
- **Lazy evaluation** - change trackers are created only when needed
- **Lambda expressions** - strongly-typed property access without magic strings

---

## 🔄 Migration Guide

### From Manual Implementation to Source Generator

If you have existing manual implementations, migrating is straightforward:

**Before (Manual):**

```csharp
public class Person : ITrackable<Person>
{
    private string _name;
    public string Name
    {
        get => _name;
        set { this.GetChangeTracker().RecordChange(_name, value); _name = value; }
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

### From Other Change Tracking Libraries

Common migration patterns from popular libraries:

**From INotifyPropertyChanged:**

```csharp
// Before
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

// After
[Trackable]
public partial class Person
{
    public partial string Name { get; set; }
}
```

---

## ❓ Frequently Asked Questions

### Q: Can I use this with Entity Framework?

A: Absolutely! Err.ChangeTracking complements Entity Framework's change tracking. You can use it in you business layer,
of for DTOs, ViewModels, or when you need more granular control over change detection.

### Q: Does it work with records?

A: Yes! `record class` is fully supported with the source generator, but `record struct` not supported, be cause it is
value type .

### Q: What about performance compared to manual implementation?

A: The generated code is nearly identical to hand-written manual implementation, with zero runtime overhead.

### Q: Can I track changes in inherited properties?

A: Yes, inheritance is fully supported. Mark the base class as `[Trackable]` and derived classes will inherit change
tracking capabilities.

### Q: Is it thread-safe?

A: The change tracker itself is not thread-safe by design (for performance). If you need thread-safety, implement
appropriate locking mechanisms in your application.

### Q: Can I disable tracking temporarily?

A: Yes, use `GetChangeTracker().Enable(false)` to temporarily disable tracking.

---

## 🐛 Troubleshooting

### Common Issues and Solutions

**Issue: Properties not being tracked**

- Ensure the class is marked with `[Trackable]`
- Ensure properties are declared as `partial`
- Check that the property has a setter
- Verify the property isn't marked with `[NotTracked]`

**Issue: Deep tracking not working**

- Ensure nested objects implement `ITrackable<T>` or are marked with `[Trackable]`
- Add `[DeepTracking]` attribute to properties that should be deeply tracked
- Call `IsDirty(deepTracking: true)` to check deep changes

**Issue: Collection changes not detected**

- Use `[TrackCollection]` attribute on collection properties
- Ensure you're using the property (not the backing field) when modifying collections
- Call `.AsTrackable()` on the root entity

**Issue: Source generator not running**

- Ensure you have both packages installed: `Err.ChangeTracking` and `Err.ChangeTracking.SourceGenerator`
- Clean and rebuild your solution
- Check that your project targets .NET 8+ with C# 13 language version

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