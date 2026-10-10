# Embed Library .NET
Embed your .NET dependencies directly into your executable.

## 🌐 Compatibility & Target Frameworks
**EmbedLibrary** targets and modifies .NET assemblies at the ECMA-335 metadata level using `dnlib`, making it compatible with a wide range of .NET runtimes:

### Target Frameworks
- **.NET Framework**
- **.NET Core**
- **.NET (Modern)**
- **Mono / Unity**

### Supported Dependency Types
- **Managed Assemblies (`.dll`):** C#, VB.NET, and F# compiled assemblies.
- **IL Architecture:** `Any CPU`, `x86`, `x64`, and `ARM64`.

> ⚠️ **Important Notes & Limitations:**
> - **Managed Only:** Supports C#, F#, and VB.NET assemblies. Native C/C++ DLLs (e.g., SQLite wrappers) cannot be loaded directly into RAM.
> - **No Native AOT:** Not compatible with Native AOT (`<PublishAot>true</PublishAot>`), as AOT disables dynamic metadata and assembly reflection at runtime.

## 🚀 Features
- ✅ Works with **all .NET versions**
- ✅ **Post-build** tool (no build process changes)
- ✅ **Compresses assemblies** before embedding
- ✅ **Memory-only loading** (no DLLs dropped to disk)
- ✅ **Flexible library selection** (manual or auto-detect)

## 🔎 Comparison
| Tool | Build-time | Post-build | Memory Loading | Compression | Flexibility |
|---|:---:|:---:|:---:|:---:|:---:|
| **EmbedLibrary** | ❌ | ✅ | ✅ | ✅ | Manual + Auto |
| **Costura.Fody** | ✅ | ❌ | ✅ | ✅ | Auto only |
| **ILMerge** | ❌ | ✅ | ❌ | ❌ | Manual only |

## 📌 Credits
- [dnlib](https://github.com/0xd4d/dnlib) — Reads and writes .NET PE assemblies and modules.
