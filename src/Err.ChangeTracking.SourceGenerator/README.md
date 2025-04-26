# Err.ChangeTracking.SourceGenerator

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.SourceGenerator.svg)](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> A powerful Roslyn Source Generator to make your POCO models change-trackable automatically at compile time — no reflection, no proxies, blazing fast.

---

## 📚 Overview

`Err.ChangeTracking.SourceGenerator` leverages Roslyn to generate lightweight change tracking logic for POCO models **at compile-time**.

✅ Forget runtime proxies.
✅ Forget heavy reflection.
✅ Enjoy AOT-ready, ultra-performant change tracking.

Designed to work perfectly with [Err.ChangeTracking](https://www.nuget.org/packages/Err.ChangeTracking) runtime.

---

## ✨ Features

- Auto-generate change tracking logic at compile-time
- Track individual property modifications
- Supports rollback, accept changes, dirty detection
- Works with POCO classes, generic types, records
- Supports `[TrackOnly]`, `[NotTracked]`, `[TrackCollection]`
- Zero runtime overhead
- NativeAOT compatible

---

## 🚀 Quick Example

### Step 1: Annotate your model

```csharp
[Trackable]
public partial class Invoice
{
    public partial string Customer { get; set; }
    public partial decimal Amount { get; set; }

    [NotTracked]
    public partial DateTime CreatedDate { get; set; }

    [TrackCollection]
    public partial List<string> Items { get; set; }
}
```

### Step 2: Use it naturally

```csharp
var invoice = new Invoice { Customer = "Acme", Amount = 100m, Items = new List<string>() }.AsTrackable();

invoice.Customer = "NewCorp";

if (invoice.GetChangeTracker().IsDirty)
{
    Console.WriteLine($"Modified properties: {string.Join(", ", invoice.GetChangeTracker().GetOriginalValues().Keys)}");
}

invoice.GetChangeTracker().Rollback(x => x.Customer);
```

---

## 📦 Installation

```bash
dotnet add package Err.ChangeTracking.SourceGenerator
```

Requires .NET 8.0 or later.

---

## 🛠 How It Works

At compile time, the source generator analyzes all classes annotated with `[Trackable]`, and generates for each property:

- A backing field
- `get` and `set` accessors
- Automatic call to `RecordChange` inside `set`

No need for runtime reflection. Everything is ready at build.

---

## 📋 License

Licensed under the [MIT License](LICENSE).

---

## 🔥 Related Projects

- [Err.ChangeTracking](https://www.nuget.org/packages/Err.ChangeTracking) — Runtime library for tracking and rollback.

---

> Built with ❤️ by **ERRADIL**
