# Err.ChangeTracking Solution (Full Documentation)

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.svg)](https://www.nuget.org/packages/Err.ChangeTracking)
[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.SourceGenerator.svg)](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator)

> A complete, high-performance, AOT-friendly solution to track changes on POCO models — combining runtime efficiency and compile-time generation.

---

# 📚 Introduction

Managing changes on POCO models is a common but tedious problem. Manually tracking modifications leads to verbose code, maintenance headaches, and runtime inefficiency.

**Err.ChangeTracking** provides:
- A lightweight **runtime** library.
- A powerful **Roslyn Source Generator** to automate everything at **compile time**.

Result?  
⚡ Lightning-fast, zero-reflection change tracking ready for Blazor, NativeAOT, Cloud, Web, and Mobile.

---

# 🚨 Problem Statement

✅ How to detect if a POCO model has been modified?  
✅ How to rollback changes easily?  
✅ How to track changes without relying on slow reflection or dynamic proxies?

Conventional solutions introduce runtime overhead and are not AOT-friendly.

---

# 🛠 Manual Implementation Example (Using Only `Err.ChangeTracking` Runtime)

```csharp
using System;
using Err.ChangeTracking;

public partial class Person : ITrackable<Person>
{
    private IChangeTracking<Person> _changeTracker;
    public IChangeTracking<Person> GetChangeTracker() => _changeTracker ??= new ChangeTracking<Person>(this);

    private string _firstName;
    public partial string FirstName
    {
        get => _firstName;
        set { _changeTracker?.RecordChange(nameof(FirstName), _firstName, value); _firstName = value; }
    }

    private int _age;
    public partial int Age
    {
        get => _age;
        set { _changeTracker?.RecordChange(nameof(Age), _age, value); _age = value; }
    }
}
```

**Explanation**:
- `_changeTracker` monitors the original values.
- `RecordChange` is called manually inside each setter.
- `IsDirty`, `Rollback`, and `AcceptChanges` become available.

✅ Powerful — but ✋ very repetitive and error-prone for large models.

---

# 🤖 With Err.ChangeTracking.SourceGenerator (Recommended)

Instead of manually writing setters, just annotate your class:

```csharp
[Trackable]
public partial class Person
{
    public partial string FirstName { get; set; }
    public partial int Age { get; set; }
}
```

The **source generator** will auto-generate the backing fields, tracking logic, and setter wrappers.

✅ Keep your models clean, readable, and efficient.

---

# ✨ How the Generator Works

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

---

# 🧪 Examples From Unit Tests

## Tracking simple properties

```csharp
var person = new Person { FirstName = "Alice", Age = 30 }.AsTrackable();
person.FirstName = "Bob";

Assert.True(person.GetChangeTracker().IsDirty);
Assert.True(person.GetChangeTracker().HasChanged(x => x.FirstName));
```

## Rollback a single property

```csharp
person.GetChangeTracker().Rollback(x => x.FirstName);
Assert.Equal("Alice", person.FirstName);
```

## Tracking collections

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

## TrackingMode OnlyMarked with [TrackOnly]

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

---

# 🚀 Typical Use Cases

- Change tracking in CRUD applications
- Undo/Redo features
- Form validation and dirty detection
- Optimizing entity update operations

---

# 📦 Requirements

**IMPORTANT:** This package uses C# 13 feature _**Patrial properties**_ and requires either:

- .NET 9.0 or higher, OR
- .NET 8.0 with LangVersion set to "preview" in your project file

If you're using .NET 8.0, add the following to your project file:

``` xml
<PropertyGroup>
    <LangVersion>preview</LangVersion>
</PropertyGroup>
```

! Without this configuration, the source generator will not work correctly.

# 📦 Installation

```bash
dotnet add package Err.ChangeTracking

dotnet add package Err.ChangeTracking.SourceGenerator
```


---

# 📋 License

Licensed under the [MIT License](LICENSE).

---

# 🔥 Related Projects

- [Err.ChangeTracking](https://www.nuget.org/packages/Err.ChangeTracking) — Runtime library
- [Err.ChangeTracking.SourceGenerator](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator) — Roslyn code generator

---

# 🙌 Contributions

Contributions are welcome!  
Fork the repository, submit a PR, or open an issue to suggest improvements!

---

> Built with ❤️ by **ERRADIL**
