# Err.ChangeTracking: Effortless Property Change Tracking for .NET

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.svg)](https://www.nuget.org/packages/Err.ChangeTracking)
[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.SourceGenerator.svg)](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator)

## 🚀 The Problem We Solve

Have you ever faced these challenges in your .NET applications?

- **No Entity Framework available**, but you still need to track object changes
- **Overhead of reflection-based change tracking** slowing down your application
- **Complex manual property change notification** code cluttering your models
- Need for **AOT compilation support** with Blazor, MAUI, or NativeAOT

Traditional change tracking solutions involve verbose boilerplate code or reflection-based approaches that aren't
AOT-friendly. We needed something better.

## 💡 The Solution: Compile-Time Change Tracking

**Err.ChangeTracking** leverages C# 13's revolutionary **partial properties** feature to provide:

- **Zero-reflection** change tracking with minimal runtime overhead
- **Compile-time code generation** that integrates naturally with your models
- Full **AOT compatibility** for all modern .NET platforms
- Simple, clean model classes with powerful tracking capabilities

## ✨ Key Features

- **Track property changes** without manual PropertyChanged events
- **Collection tracking** for Lists and Dictionaries
- **Rollback changes** to original values with a single method call
- **Selectively track properties** using attributes
- **Zero runtime reflection** for maximum performance
- **100% AOT compatible** for all modern .NET scenarios

Example of auto-tracking:

```csharp
[Trackable]
public partial class Person
{
    public partial string Name { get; set; } // Auto-tracked!
}

var person = new Person { Name = "Alice" }.AsTrackable();
person.Name = "Bob";
bool hasChanged = person.GetChangeTracker().IsDirty; // True
```

## 🔮 Why Now?

This solution is made possible by C# 13's **partial properties** feature. Before this innovation, generating property
implementations with specialized setters required complex runtime proxies or reflection-based approaches. Now we can:

1. Generate efficient, customized property implementations at compile time
2. Maintain clean, simple model definitions
3. Achieve zero-reflection tracking for AOT compatibility
4. Keep runtime overhead at absolute minimum

## 📋 Requirements

This library requires either:

- **.NET 9.0** or higher (recommended), OR
- **.NET 8.0** with `<LangVersion>preview</LangVersion>` in your project file

The magic happens through C# 13's **partial properties** feature, which unlocks unprecedented compile-time generation
capabilities.

## 📦 Installation

```shell script
dotnet add package Err.ChangeTracking
dotnet add package Err.ChangeTracking.SourceGenerator
```

## 🔍 Simple Example: Before & After

Our lightweight solution offers two approaches to change tracking:

### Manual Implementation (Fine-grained control when needed)

You can manually implement change tracking with just one line in your property setters:

```csharp
using Err.ChangeTracking;

public partial class Person : ITrackable<Person>
{
    private string _firstName;
    public partial string FirstName
    {
        get => _firstName;
        set { this.GetChangeTracker().RecordChange(nameof(FirstName), _firstName, value); _firstName = value; }
    }
    // ...
}
```

For complex entities with nested objects, you can also manually configure deep tracking in a static constructor. This
enables tracking changes through entire object graphs:

```csharp
static Order()
{
    // Get all properties that are either deep trackable entities or collections
    DeepTracking<Model>.SetTrackableProperties([
        x => x.Shipping?.GetChangeTracker(), // if property is Trackable<T>
        x => x.Items as IBaseTracker // if property Is ITrackableCollection
    ]);
}
```
This manual approach gives you precise control over when and how changes are recorded, perfect for complex scenarios
where you need custom validation, notification, or conditional tracking logic.

### Automated with Source Generator (Recommended)

Thanks to our source generator, you can achieve the same functionality with minimal code:

```csharp
[Trackable]
public partial class Person
{
    public partial string FirstName { get; set; } // Will be tracked
    public partial int Age { get; set; } // Will be tracked too
}
```

The source generator automatically implements:

- The `ITrackable<>` interface
- Backing fields for properties
- Property setters with change tracking
- All the tracking infrastructure

## 🛠️ How the Generator Works

- Class must be marked as `[Trackable]`
- Class must be `partial`
- Public, `partial` properties are eligible
- Attributes control behavior:

| Attribute | Behavior |
|-----------|----------|
| `[TrackOnly]` | Track this property only (when using `TrackingMode.OnlyMarked`) |
| `[NotTracked]` | Exclude this property from tracking |
| `[TrackCollection]` | Track changes inside List/Dictionary items |
| `TrackingMode.All` | (default) track all eligible properties |
| `TrackingMode.OnlyMarked` | Only track `[TrackOnly]` properties |

## 🧪 Examples From Unit Tests

### Tracking simple properties

```csharp
var person = new Person { FirstName = "Alice", Age = 30 }.AsTrackable();
person.FirstName = "Bob";

Assert.True(person.GetChangeTracker().IsDirty);
Assert.True(person.GetChangeTracker().HasChanged(x => x.FirstName));
```

### Rollback a single property

```csharp
person.GetChangeTracker().Rollback(x => x.FirstName);
Assert.Equal("Alice", person.FirstName);
```

### Tracking collections

```csharp
[Trackable]
public partial class Order
{
    [TrackCollection]
    public partial List<string> Items { get; set; }
}

var order = new Order { Items = new List<string>() }.AsTrackable();
order.Items.AsTrackable().Add("New Item");

Assert.True(order.GetChangeTracker().IsDirty);
```

### TrackingMode OnlyMarked with [TrackOnly]

```csharp
[Trackable(Mode = TrackingMode.OnlyMarked)]
public partial class Invoice
{
    [TrackOnly]
    public partial string InvoiceNumber { get; set; }

    public partial string Comment { get; set; }
}

var invoice = new Invoice { InvoiceNumber = "INV001", Comment = "Test" }.AsTrackable();

invoice.InvoiceNumber = "INV002";
invoice.Comment = "Changed Comment";

Assert.True(invoice.GetChangeTracker().HasChanged(x => x.InvoiceNumber));
Assert.False(invoice.GetChangeTracker().HasChanged(x => x.Comment));
```

## 🏁 Conclusion

Err.ChangeTracking provides an elegant, modern solution to an age-old problem. It's perfect for applications that:

- Need change tracking without Entity Framework
- Require AOT compilation support
- Want to avoid complex INotifyPropertyChanged implementations
- Need optimized, lightweight change detection

Start using Err.ChangeTracking today and experience the power of compile-time property tracking with zero runtime
reflection!

## 📝 License

Licensed under the [MIT License](LICENSE).

---

## 🚀 Bonus: More Typical Use Cases

### 3. Form Validation and Dirty Detection

Enhance UX by tracking user changes in forms:

```csharp
// In a Blazor component
@code {
    private UserProfileForm _form = new UserProfileForm().AsTrackable();
    
    private async Task SaveChanges()
    {
        if (!_form.GetChangeTracker().IsDirty)
        {
            NotificationService.Info("No changes to save");
            return;
        }
        
        var changedProperties = _form.GetChangeTracker().GetChangedProperties();
        // Validate only changed fields ...
        
        // Save changes
        await UserService.UpdateProfileAsync(_form);
        _form.GetChangeTracker().AcceptChanges();
        
        NotificationService.Success("Profile updated successfully");
    }
    
    private void DiscardChanges()
    {
        if (_form.GetChangeTracker().IsDirty)
        {
            _form.GetChangeTracker().Rollback();
            NotificationService.Info("Changes discarded");
        }
    }
}
```

### 1. Smart Repository Operations

Optimize database operations by only updating what changed:

```csharp
public class OrderRepository
{
    public async Task SaveAsync(Order order)
    {
        var tracker = order.GetChangeTracker();
        
        if (!tracker.IsDirty)
            return; // Nothing to save
        
        // Get only the changed properties
        var changes = tracker.GetChangedProperties();
        
        // Build SQL with only changed columns
        var updateColumns = string.Join(", ", changes.Select(p => $"{p} = @{p}"));
        var sql = $"UPDATE Orders SET {updateColumns} WHERE Id = @Id";
        
        await _db.ExecuteAsync(sql, order);
        tracker.AcceptChanges();
    }
}
```

### 2. Domain Events with Change Detection

Publish specific events based on detected changes:

```csharp
public class OrderService
{
    public async Task UpdateOrderAsync(Order order)
    {
        await _repository.SaveAsync(order);
        
        var tracker = order.GetChangeTracker();
        
        if (tracker.HasChanged(x => x.Status))
        {
            var originalStatus = (OrderStatus)tracker.GetOriginalValues()[nameof(Order.Status)];
            _eventPublisher.Publish(new OrderStatusChangedEvent(
                order.Id, originalStatus, order.Status
            ));
        }
    }
}
```

---

> Built with ❤️ by **ERRADIL**