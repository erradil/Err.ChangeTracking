# Err.ChangeTracking

[![NuGet](https://img.shields.io/nuget/v/Err.ChangeTracking.svg)](https://www.nuget.org/packages/Err.ChangeTracking)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> Lightweight runtime library for tracking property changes on POCO models.

---

## 📚 Overview

`Err.ChangeTracking` is a minimal, fast, and AOT-compatible library that provides runtime support for tracking property changes on POCO classes.
It is designed to work seamlessly with [Err.ChangeTracking.SourceGenerator](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator), but can be reused independently.

---

## ✨ Features

- Track property changes at runtime
- Rollback individual properties or entire objects
- Accept changes manually
- Provides extensible `IChangeTracking<T>` API
- Supports advanced scenarios: `List<T>`, `Dictionary<K,V>`, collections tracking
- NativeAOT, Blazor, WASM ready
- Zero reflection during tracking

---

## 🚀 Quick Example

```csharp
public partial class Person : ITrackable<Person>
{
    private IChangeTracking<Person> _tracker;
    public IChangeTracking<Person> GetChangeTracker() => _tracker ??= new ChangeTracking<Person>(this);

    public partial string Name { get; set; }
    public partial int Age { get; set; }
}

var person = new Person { Name = "Alice", Age = 25 }.AsTrackable();
person.Name = "Bob";

if (person.GetChangeTracker().IsDirty)
{
    Console.WriteLine("Person has changed!");
}

person.GetChangeTracker().Rollback(x => x.Name);
```

---

## 📦 Installation

```bash
dotnet add package Err.ChangeTracking
```

Or via NuGet Package Manager.

---

## 🛠 Advanced Usage

- `Rollback()` ➔ Rollback all changes
- `Rollback(x => x.Property)` ➔ Rollback a specific property
- `AcceptChanges()` ➔ Accept all current values
- `AcceptChanges(x => x.Property)` ➔ Accept a specific property
- `HasChanged(x => x.Property)` ➔ Check if a specific property has changed

Supports `List<T>`, `Dictionary<TKey, TValue>`, and custom `TrackableList<T>` or `TrackableDictionary<K,V>` extensions.

---

## 📋 License

Licensed under the [MIT License](LICENSE).

---

## 🔥 Related Projects

- [Err.ChangeTracking.SourceGenerator](https://www.nuget.org/packages/Err.ChangeTracking.SourceGenerator) - Roslyn Source Generator for compile-time change tracking generation.

---

> Built with ❤️ by **ERRADIL**
