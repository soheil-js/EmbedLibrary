using dnlib.DotNet;
using dnlib.Load;

namespace dnlib.Extensions
{
    public static class Extension
    {
        public static AssemblyContext GetAssemblyContext(this ModuleDefMD module)
        {
            return new AssemblyContext(module);
        }
    }
}
