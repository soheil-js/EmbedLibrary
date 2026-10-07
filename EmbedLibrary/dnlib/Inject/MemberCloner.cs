using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Creates empty ("skeleton") copies of dnlib definitions. A clone only carries the
    /// structural information needed to add the definition to the target module; bodies,
    /// custom attributes and cross-references are filled in later by <see cref="MemberCopier"/>.
    /// </summary>
    internal sealed class MemberCloner
    {
        private readonly InjectMapper _mapper;

        public MemberCloner(InjectMapper mapper)
        {
            _mapper = mapper;
        }

        public TypeDefUser CloneType(TypeDef source)
        {
            var typeDef = new TypeDefUser(source.Namespace, source.Name, _mapper.ImportType(source.BaseType))
            {
                Attributes = source.Attributes,
                ClassLayout = source.ClassLayout != null
                    ? new ClassLayoutUser(source.ClassLayout.PackingSize, source.ClassSize)
                    : null
            };

            foreach (GenericParam genericParam in source.GenericParameters)
                typeDef.GenericParameters.Add(CloneGenericParam(genericParam));

            return typeDef;
        }

        public MethodDefUser CloneMethod(MethodDef source)
        {
            var methodDef = new MethodDefUser(
                source.Name,
                _mapper.ImportMethodSig(source.MethodSig),
                source.ImplAttributes,
                source.Attributes);

            foreach (GenericParam genericParam in source.GenericParameters)
                methodDef.GenericParameters.Add(CloneGenericParam(genericParam));

            return methodDef;
        }

        public PropertyDefUser CloneProperty(PropertyDef source) =>
            new PropertyDefUser(source.Name, _mapper.ImportPropertySig(source.PropertySig), source.Attributes);

        public FieldDefUser CloneField(FieldDef source) =>
            new FieldDefUser(source.Name, _mapper.ImportFieldSig(source.FieldSig), source.Attributes);

        public EventDefUser CloneEvent(EventDef source) =>
            new EventDefUser(source.Name, _mapper.ImportType(source.EventType), source.Attributes);

        public GenericParamUser CloneGenericParam(GenericParam source) =>
            new GenericParamUser(source.Number, source.Flags, source.Name);
    }
}
