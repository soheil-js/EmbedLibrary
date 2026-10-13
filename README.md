# EmbedLibrary
Embed your .NET dependencies directly into your executable.

> 🗄️**Archived:** This repository is no longer maintained. Development has moved to [Embedix](https://github.com/soheil-js/Embedix), the successor project.

## 🌐 Compatibility & Target Frameworks
**EmbedLibrary** modifies .NET assemblies at the ECMA-335 metadata level using [dnlib](https://github.com/0xd4d/dnlib), allowing managed dependencies to be embedded into the target assembly.

### Target Frameworks
- **.NET Framework**
- **.NET Core**
- **Modern .NET**

### Supported Dependency Types
- **Managed Assemblies (`.dll`):** Compiled .NET assemblies, regardless of the source language, subject to runtime and dependency compatibility.
- **Target Architectures:** `Any CPU`, `x86`, `x64`, and `ARM64`.

> ⚠️ **Important Notes & Limitations:**
> - **Managed Assemblies Only:** EmbedLibrary is designed for managed .NET assemblies. Native C/C++ DLLs cannot be loaded through the same managed assembly-resolution mechanism. Applications that depend on native libraries may require separate handling.
> - **No Native AOT:** EmbedLibrary is not compatible with Native AOT deployments because its assembly-resolution approach relies on dynamic loading of managed assemblies.
> - **Runtime Compatibility:** .NET Framework 2.0 and 3.5 are not supported.

## 🚀 Features
- ✅ Embeds managed dependencies into the target executable
- ✅ **Post-build** processing without requiring changes to the build process
- ✅ Compresses assemblies before embedding
- ✅ **In-memory loading** of embedded assemblies without extracting them to disk
- ✅ Flexible library selection: manual selection or automatic detection

## 🔎 Comparison
| Tool | Build-time | Post-build | Loads Embedded DLLs from Memory | Compression | Adds Dependencies to Target | Flexibility |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| **EmbedLibrary** | ❌ | ✅ | ✅ | ✅| ❌ | Manual + Auto |
| **Costura.Fody** | ✅ | ❌ | ✅ | ✅ | ✅ | Manual + Auto |
| **ILMerge** | ❌ | ✅ | N/A | ❌ | ❌ | Manual only |

## 📌 Credits
- [dnlib](https://github.com/0xd4d/dnlib) — Reads and writes .NET PE assemblies and modules.
