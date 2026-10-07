using System;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Thrown by <see cref="Injection.Inject"/> when a <see cref="TypeDef"/> fails to be
    /// injected into the target module. Replaces the plain <see cref="Exception"/> wrapper
    /// used by the original implementation so callers can catch injection failures
    /// specifically and still inspect <see cref="InnerException"/> for the root cause.
    /// </summary>
    [Serializable]
    public class InjectionException : Exception
    {
        /// <summary>Full name of the source type that failed to inject, if known.</summary>
        public string SourceTypeFullName { get; }

        public InjectionException(string message)
            : base(message)
        {
        }

        public InjectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public InjectionException(TypeDef source, Exception innerException)
            : base($"Failed to inject type '{source?.FullName}'.", innerException)
        {
            SourceTypeFullName = source?.FullName;
        }
    }
}
