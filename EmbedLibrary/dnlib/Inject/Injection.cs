using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Injects (deep-copies) <see cref="TypeDef"/>s - and everything they transitively
    /// reference from their own source module - into a different target <see cref="ModuleDef"/>.
    /// </summary>
    /// <remarks>
    /// This is a thin orchestration facade. The actual work is split across:
    /// <list type="bullet">
    /// <item><see cref="InjectMapper"/> - caches imports and resolves source-to-target references.</item>
    /// <item><see cref="MemberCloner"/> - creates empty skeleton copies of definitions.</item>
    /// <item><see cref="TypeInitializer"/> - builds skeletons (phase 1) and fills them in (phase 2).</item>
    /// <item><see cref="MemberCopier"/> / <see cref="MethodBodyCopier"/> - the phase-2 filling-in logic.</item>
    /// <item><see cref="ReferenceFixer"/> - defensive final pass after injection.</item>
    /// </list>
    /// </remarks>
    public class Injection
    {
        private readonly InjectMapper _mapper;
        private readonly HashSet<TypeDef> _processedTypes = new HashSet<TypeDef>();
        private readonly TypeInitializer _initializer;
        private readonly ReferenceFixer _referenceFixer;

        public Injection(ModuleDef target)
        {
            _mapper = new InjectMapper(target);

            var cloner = new MemberCloner(_mapper);
            var copier = new MemberCopier(_mapper);

            _initializer = new TypeInitializer(_mapper, cloner, copier, _processedTypes);
            _referenceFixer = new ReferenceFixer(_mapper);
        }

        /// <summary>
        /// Injects <paramref name="source"/> (and anything it references from its own
        /// module) into the target module. Does nothing if the type was already injected
        /// through this <see cref="Injection"/> instance.
        /// </summary>
        /// <remarks>
        /// Correctly resolves references between <paramref name="source"/> and its own
        /// nested types regardless of which one happens to be processed first (see
        /// <see cref="TypeInitializer.BuildSkeletonTree"/>). If you are injecting several
        /// *separate* top-level types that reference each other, use
        /// <see cref="Inject(IEnumerable{TypeDef})"/> instead so all of them get their
        /// skeletons built before any of their bodies are copied - calling this overload
        /// once per type cannot guarantee that ordering across different calls.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="InjectionException">Injection failed; see <see cref="Exception.InnerException"/>.</exception>
        public void Inject(TypeDef source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_processedTypes.Contains(source)) return;

            try
            {
                // Phase 1: skeletons for source + its whole nested-type tree.
                var target = _initializer.BuildSkeletonTree(source);

                // Phase 2: now every skeleton in this tree exists - fill them all in.
                _initializer.CopyTree(source);

                _mapper.TargetModule.Types.Add(target);

                // Defensive final pass for anything outside this tree (e.g. a type that
                // was injected earlier through a previous call).
                _referenceFixer.FixReferences(target);
                _referenceFixer.FixGenericParameters(target);

                _processedTypes.Add(source);
            }
            catch (Exception ex)
            {
                throw new InjectionException(source, ex);
            }
        }

        /// <summary>
        /// Injects a batch of top-level types together. Use this instead of calling
        /// <see cref="Inject(TypeDef)"/> once per type whenever the types being injected
        /// reference each other, regardless of which order they're given in - every
        /// skeleton in the whole batch is built before any body is copied.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="sources"/> is null.</exception>
        /// <exception cref="InjectionException">Injection failed; see <see cref="Exception.InnerException"/>.</exception>
        public void Inject(IEnumerable<TypeDef> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            var roots = sources.Where(s => s != null && !_processedTypes.Contains(s)).Distinct().ToList();
            if (roots.Count == 0) return;

            try
            {
                var targets = new List<TypeDef>(roots.Count);

                // Phase 1 for the whole batch first.
                foreach (var source in roots)
                    targets.Add(_initializer.BuildSkeletonTree(source));

                // Phase 2 for the whole batch, now that every root's skeleton exists.
                foreach (var source in roots)
                    _initializer.CopyTree(source);

                foreach (var target in targets)
                    _mapper.TargetModule.Types.Add(target);

                foreach (var target in targets)
                {
                    _referenceFixer.FixReferences(target);
                    _referenceFixer.FixGenericParameters(target);
                }

                foreach (var source in roots)
                    _processedTypes.Add(source);
            }
            catch (Exception ex)
            {
                throw new InjectionException(
                    $"Failed to inject a batch of {roots.Count} type(s). See the inner exception for details.", ex);
            }
        }

        /// <summary>
        /// Non-throwing version of <see cref="Inject(TypeDef)"/>.
        /// </summary>
        /// <remarks>
        /// Does not run <see cref="ReferenceFixer.FixReferences"/> /
        /// <see cref="ReferenceFixer.FixGenericParameters"/>, so references to types
        /// injected earlier through a *different* call may still point at the source
        /// module afterwards. References within <paramref name="source"/>'s own nested-type
        /// tree are still resolved correctly thanks to the two-phase build. Prefer
        /// <see cref="Inject(TypeDef)"/> unless you have a specific reason to avoid exceptions.
        /// </remarks>
        public bool TryInject(TypeDef source, out TypeDef target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (_processedTypes.Contains(source))
            {
                target = null;
                return false;
            }

            try
            {
                target = _initializer.BuildSkeletonTree(source);
                _initializer.CopyTree(source);
                _mapper.TargetModule.Types.Add(target);

                _processedTypes.Add(source);
                return true;
            }
            catch
            {
                target = null;
                return false;
            }
        }
    }
}
