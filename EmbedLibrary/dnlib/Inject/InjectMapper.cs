using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.Inject.Caching;

namespace dnlib.Inject
{
    /// <summary>
    /// Resolves and caches references between the source module (where types are injected
    /// from) and the target module (where they are injected into).
    ///
    /// Acts as a dnlib <see cref="ImportMapper"/> so that dnlib's own <see cref="Importer"/>
    /// redirects references that point at an already-injected definition to its injected
    /// copy, instead of importing a brand-new reference to the original assembly.
    /// </summary>
    internal sealed class InjectMapper : ImportMapper
    {
        private readonly ImportCache<ITypeDefOrRef, ITypeDefOrRef> _typeRefCache = new ImportCache<ITypeDefOrRef, ITypeDefOrRef>();
        private readonly ImportCache<Type, ITypeDefOrRef> _typeCache = new ImportCache<Type, ITypeDefOrRef>();
        private readonly ImportCache<IMemberRef, IMemberRef> _memberRefCache = new ImportCache<IMemberRef, IMemberRef>();
        private readonly ImportCache<IMethodDefOrRef, IMethodDefOrRef> _methodRefCache = new ImportCache<IMethodDefOrRef, IMethodDefOrRef>();
        private readonly ImportCache<TypeSig, TypeSig> _typeSigCache = new ImportCache<TypeSig, TypeSig>();
        private readonly ImportCache<MethodSig, MethodSig> _methodSigCache = new ImportCache<MethodSig, MethodSig>();
        private readonly ImportCache<PropertySig, PropertySig> _propertySigCache = new ImportCache<PropertySig, PropertySig>();
        private readonly ImportCache<FieldSig, FieldSig> _fieldSigCache = new ImportCache<FieldSig, FieldSig>();
        private readonly ImportCache<CallingConventionSig, CallingConventionSig> _callingConventionSigCache = new ImportCache<CallingConventionSig, CallingConventionSig>();
        private readonly ImportCache<ICustomAttributeType, ICustomAttributeType> _customAttributeTypeCache = new ImportCache<ICustomAttributeType, ICustomAttributeType>();
        private readonly ImportCache<IType, IType> _itypeCache = new ImportCache<IType, IType>();
        private readonly ImportCache<IMethod, IMethod> _methodCache = new ImportCache<IMethod, IMethod>();
        private readonly ImportCache<IField, IField> _fieldCache = new ImportCache<IField, IField>();

        // Lazily (re)built index over DnlibDefMaps, keyed by the source member's MDToken.
        // The original implementation did `DnlibDefMaps.Keys.FirstOrDefault(...)` on every
        // single Map(...) call, which is O(n) per lookup and O(n^2) overall for a type with
        // many members. Rebuilding this index only when DnlibDefMaps has actually grown
        // keeps lookups effectively O(1) once skeleton-building for a type is done.
        private Dictionary<MDToken, IMemberRef> _tokenIndex;
        private int _tokenIndexVersion = -1;

        /// <summary>
        /// Maps every already-injected source definition (TypeDef/MethodDef/FieldDef/...)
        /// to the definition that was created for it in <see cref="TargetModule"/>.
        /// </summary>
        public Dictionary<IMemberRef, IMemberRef> DnlibDefMaps { get; } = new Dictionary<IMemberRef, IMemberRef>();

        /// <summary>The module definitions are being injected into.</summary>
        public ModuleDef TargetModule { get; }

        /// <summary>The dnlib importer used to bring foreign references into <see cref="TargetModule"/>.</summary>
        public Importer Importer { get; }

        public InjectMapper(ModuleDef target)
        {
            TargetModule = target ?? throw new ArgumentNullException(nameof(target));
            Importer = new Importer(target, ImporterOptions.TryToUseTypeDefs, default, this);
        }

        #region ImportMapper overrides

        public override ITypeDefOrRef Map(ITypeDefOrRef source)
        {
            if (source == null) return null;

            if (_typeRefCache.TryGetValue(source, out var cachedType))
                return cachedType;

            return ResolveByToken(source.MDToken) as ITypeDefOrRef;
        }

        public override IMethod Map(MethodDef source)
        {
            if (source == null) return null;

            if (_methodRefCache.TryGetValue(source, out var cachedMethod))
                return cachedMethod;

            if (_memberRefCache.TryGetValue(source, out var cachedMember))
                return cachedMember as IMethod;

            return ResolveByToken(source.MDToken) as IMethod;
        }

        public override IField Map(FieldDef source)
        {
            if (source == null) return null;

            if (_memberRefCache.TryGetValue(source, out var cachedMember))
                return cachedMember as IField;

            return ResolveByToken(source.MDToken) as IField;
        }

        public override MemberRef Map(MemberRef source)
        {
            if (source == null) return null;

            if (_memberRefCache.TryGetValue(source, out var cachedMember))
                return cachedMember as MemberRef;

            return ResolveByToken(source.MDToken) as MemberRef;
        }

        public override ITypeDefOrRef Map(Type source)
        {
            if (source == null) return null;

            if (_typeCache.TryGetValue(source, out var cachedType))
                return cachedType;

            var result = ResolveByMetadataToken(source.MetadataToken) as ITypeDefOrRef;
            if (result != null)
                _typeCache.GetOrAdd(source, _ => result);

            return result;
        }

        #endregion

        #region Import helpers (used while cloning/copying injected members)

        public ITypeDefOrRef ImportType(ITypeDefOrRef type) =>
            _typeRefCache.GetOrAdd(type, t => Importer.Import(t));

        public IMemberRef ImportMember(IMemberRef member) =>
            _memberRefCache.GetOrAdd(member, ImportMemberCore);

        public IMethodDefOrRef ImportMethod(IMethodDefOrRef method) =>
            _methodRefCache.GetOrAdd(method, m => Importer.Import(m) as IMethodDefOrRef);

        public TypeSig ImportTypeSig(TypeSig sig) =>
            _typeSigCache.GetOrAdd(sig, Importer.Import);

        public MethodSig ImportMethodSig(MethodSig sig) =>
            _methodSigCache.GetOrAdd(sig, Importer.Import);

        public PropertySig ImportPropertySig(PropertySig sig) =>
            _propertySigCache.GetOrAdd(sig, Importer.Import);

        public FieldSig ImportFieldSig(FieldSig sig) =>
            _fieldSigCache.GetOrAdd(sig, Importer.Import);

        public CallingConventionSig ImportCallingConventionSig(CallingConventionSig sig) =>
            _callingConventionSigCache.GetOrAdd(sig, Importer.Import);

        public ICustomAttributeType ImportCustomAttributeType(ICustomAttributeType attributeType) =>
            _customAttributeTypeCache.GetOrAdd(attributeType, t => (ICustomAttributeType)Importer.Import(t));

        public IType ImportType(IType type) =>
            _itypeCache.GetOrAdd(type, Importer.Import);

        public IMethod ImportMethod(IMethod method) =>
            _methodCache.GetOrAdd(method, Importer.Import);

        public IField ImportField(IField field) =>
            _fieldCache.GetOrAdd(field, Importer.Import);

        #endregion

        private IMemberRef ImportMemberCore(IMemberRef member)
        {
            switch (member)
            {
                case MemberRef memberRef:
                    return Importer.Import(memberRef);
                case MethodDef methodDef:
                    return Importer.Import(methodDef);
                case FieldDef fieldDef:
                    return Importer.Import(fieldDef);
                default:
                    throw new NotSupportedException($"Unsupported member type: {member.GetType().FullName}");
            }
        }

        private IMemberRef ResolveByToken(MDToken token)
        {
            EnsureTokenIndex();
            return _tokenIndex.TryGetValue(token, out var result) ? result : null;
        }

        private IMemberRef ResolveByMetadataToken(int token)
        {
            EnsureTokenIndex();
            foreach (var entry in _tokenIndex)
            {
                if (entry.Key.ToInt32() == token)
                    return entry.Value;
            }
            return null;
        }

        private void EnsureTokenIndex()
        {
            if (_tokenIndex != null && _tokenIndexVersion == DnlibDefMaps.Count)
                return;

            _tokenIndex = new Dictionary<MDToken, IMemberRef>();
            foreach (var entry in DnlibDefMaps)
                _tokenIndex[entry.Key.MDToken] = entry.Value;

            _tokenIndexVersion = DnlibDefMaps.Count;
        }
    }
}
