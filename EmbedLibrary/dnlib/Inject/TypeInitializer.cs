using System.Collections.Generic;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Builds skeleton copies of a <see cref="TypeDef"/> tree and fills them in, split into
    /// two explicit phases on purpose - see the remarks on <see cref="BuildSkeletonTree"/>.
    /// </summary>
    internal sealed class TypeInitializer
    {
        private readonly InjectMapper _mapper;
        private readonly MemberCloner _cloner;
        private readonly MemberCopier _copier;
        private readonly HashSet<TypeDef> _processedTypes;

        public TypeInitializer(InjectMapper mapper, MemberCloner cloner, MemberCopier copier, HashSet<TypeDef> processedTypes)
        {
            _mapper = mapper;
            _cloner = cloner;
            _copier = copier;
            _processedTypes = processedTypes;
        }

        /// <summary>
        /// Phase 1. Recursively creates an empty ("skeleton") clone of <paramref name="source"/>
        /// and every one of its nested types, registering each one in
        /// <see cref="InjectMapper.DnlibDefMaps"/>. Does NOT copy any bodies, signatures,
        /// base types or custom attributes.
        /// </summary>
        /// <remarks>
        /// This must run to completion for an entire type tree - and ideally for every root
        /// being injected in the same batch - before <see cref="CopyTree"/> runs for any of
        /// them.
        ///
        /// Reason: dnlib's Importer asks InjectMapper ("Map(...)") to redirect a reference to
        /// its injected copy at the exact moment that reference is imported. If the
        /// referenced type/method/field hasn't been registered in
        /// <see cref="InjectMapper.DnlibDefMaps"/> yet, Map returns null and the Importer
        /// falls back to creating a brand-new reference into the *original* source module -
        /// which then gets cached as the "resolved" value for that member and is never
        /// revisited, even once the real target copy exists. Concretely: if nested type A's
        /// method body calls nested type B, and A is processed (skeleton + body) before B
        /// even has a skeleton, A's call instruction permanently ends up pointing at the
        /// source assembly's B instead of the injected one. Building every skeleton first
        /// (this method) before copying any body (<see cref="CopyTree"/>) removes that
        /// ordering dependency entirely: whichever member is copied first, every other
        /// member it could reference within the tree already has a target skeleton to
        /// resolve to.
        /// </remarks>
        public TypeDef BuildSkeletonTree(TypeDef source)
        {
            if (_mapper.DnlibDefMaps.TryGetValue(source, out var existing))
                return (TypeDef)existing;

            var target = _cloner.CloneType(source);
            _mapper.DnlibDefMaps[source] = target;

            foreach (TypeDef nestedType in source.NestedTypes)
            {
                if (_processedTypes.Contains(nestedType))
                    continue;

                target.NestedTypes.Add(BuildSkeletonTree(nestedType));
                _processedTypes.Add(nestedType);
            }

            foreach (var eventDef in source.Events)
            {
                var clone = _cloner.CloneEvent(eventDef);
                target.Events.Add(clone);
                _mapper.DnlibDefMaps[eventDef] = clone;
            }

            foreach (MethodDef method in source.Methods)
            {
                var clone = _cloner.CloneMethod(method);
                target.Methods.Add(clone);
                _mapper.DnlibDefMaps[method] = clone;
            }

            foreach (FieldDef field in source.Fields)
            {
                var clone = _cloner.CloneField(field);
                target.Fields.Add(clone);
                _mapper.DnlibDefMaps[field] = clone;
            }

            foreach (var property in source.Properties)
            {
                var clone = _cloner.CloneProperty(property);
                target.Properties.Add(clone);
                _mapper.DnlibDefMaps[property] = clone;
            }

            return target;
        }

        /// <summary>
        /// Phase 2. Fills in <paramref name="source"/> and every one of its nested types
        /// (base type, interfaces, signatures, custom attributes and method bodies). Only
        /// call this after <see cref="BuildSkeletonTree"/> has already run for every type
        /// that could be referenced from within this tree (see the remarks above).
        /// </summary>
        public void CopyTree(TypeDef source)
        {
            _copier.CopyAll(source, copyTypeAttributes: true);

            foreach (TypeDef nestedType in source.NestedTypes)
                CopyTree(nestedType);
        }
    }
}
