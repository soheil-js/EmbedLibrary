# Embed Library .NET
Embed your .NET dependencies directly into your executable – **no installation, no extra DLLs**.  
Runs **after compilation**, so it works with **any obfuscation tool** without interfering.  

## 🚀 Features
- ✅ Works with **all .NET versions**  
- ✅ **Post-build** tool (no build process changes)  
- ✅ **Compresses assemblies** before embedding  
- ✅ **Memory-only loading** (no DLLs dropped to disk)  
- ✅ **Flexible library selection** (manual or auto-detect)  

## 🔎 Comparison

| Tool            | Build Process | Memory Loading | Compression | Flexibility |
|-----------------|--------------|----------------|-------------|-------------|
| **EmbedLibrary**| Post-build   | ✅ Yes         | ✅ Yes      | ✅ Manual + Auto |
| **Costura.Fody**| Build-time   | ✅ Yes         | ✅ Yes (enabled by default, can disable via option) | ❌ Auto only |
| **ILMerge**     | Post-build   | ❌ No          | ❌ No       | ❌ Manual only (Windows / .NET Framework focused) |



# :pushpin:Credits
- [dnlib](https://github.com/0xd4d/dnlib) (Reads and writes .NET assemblies and modules)
