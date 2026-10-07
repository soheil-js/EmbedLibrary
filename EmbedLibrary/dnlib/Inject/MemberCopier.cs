using System.Linq;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Fills in the details (base type, interfaces, signatures, custom attributes and
    /// method bodies) of definitions that have already been cloned into the target module
    /// and registered in <see cref="InjectMapper.DnlibDefMaps"/>.
    /// </summary>
    internal sealed class MemberCopier
    {
        private readonly InjectMapper _mapper;
        private readonly MethodBodyCopier _bodyCopier;

        public MemberCopier(InjectMapper mapper)
        {
            _mapper = mapper;
            _bodyCopier = new MethodBodyCopier(mapper);
        }

        /// <summary>
        /// Copies every member of <paramref name="source"/> (and, if
        /// <paramref name="copyTypeAttributes"/> is true, the type itself: base type and
        /// interfaces) into its injected counterpart.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT recurse into nested types. In the original implementation
        /// nested types were copied twice: once by the caller's own recursion while building
        /// the skeleton, and a second time by this method's own nested-type loop - which
        /// silently duplicated every custom attribute on a nested type's members. Callers
        /// (see <see cref="TypeInitializer"/>) are expected to invoke this once per type.
        /// </remarks>
        public void CopyAll(TypeDef source, bool copyTypeAttributes)
        {
            if (copyTypeAttributes)
                CopyTypeAttributes(source);

            foreach (MethodDef method in source.Methods)
                CopyMethodDef(method);

            foreach (FieldDef field in source.Fields)
                CopyFieldDef(field);

            foreach (PropertyDef property in source.Properties)
                CopyPropertyDef(property);

            foreach (EventDef eventDef in source.Events)
                CopyEventDef(eventDef);
        }

        private void CopyTypeAttributes(TypeDef source)
        {
            var target = (TypeDef)_mapper.DnlibDefMaps[source];

            target.BaseType = _mapper.Importer.Import(source.BaseType);

            foreach (InterfaceImpl iface in source.Interfaces)
                target.Interfaces.Add(new InterfaceImplUser(_mapper.Importer.Import(iface.Interface)));
        }

        private void CopyMethodDef(MethodDef source)
        {
            var target = (MethodDef)_mapper.DnlibDefMaps[source];

            target.Signature = _mapper.ImportCallingConventionSig(source.Signature);
            target.Parameters.UpdateParameterTypes();

            if (source.ImplMap != null)
            {
                target.ImplMap = new ImplMapUser(
                    new ModuleRefUser(_mapper.TargetModule, source.ImplMap.Module.Name),
                    source.ImplMap.Name,
                    source.ImplMap.Attributes);
            }

            foreach (CustomAttribute ca in source.CustomAttributes)
                target.CustomAttributes.Add(new CustomAttribute(_mapper.ImportCustomAttributeType(ca.Constructor)));

            _bodyCopier.Copy(source, target);
        }

        private void CopyFieldDef(FieldDef source)
        {
            var target = (FieldDef)_mapper.DnlibDefMaps[source];
            target.Signature = _mapper.Importer.Import(source.Signature);
        }

        private void CopyPropertyDef(PropertyDef source)
        {
            var target = (PropertyDef)_mapper.DnlibDefMaps[source];

            if (source.SetMethod != null && source.SetMethod.HasBody)
                target.SetMethod = target.DeclaringType.Methods.First(m => m.Name == $"set_{source.Name}");

            if (source.GetMethod != null && source.GetMethod.HasBody)
                target.GetMethod = target.DeclaringType.Methods.First(m => m.Name == $"get_{source.Name}");
        }

        private void CopyEventDef(EventDef source)
        {
            var target = (EventDef)_mapper.DnlibDefMaps[source];

            target.EventType = _mapper.ImportType(source.EventType);

            if (source.AddMethod != null)
                target.AddMethod = FindCopiedMethod(target, source.AddMethod.Name);

            if (source.RemoveMethod != null)
                target.RemoveMethod = FindCopiedMethod(target, source.RemoveMethod.Name);

            if (source.InvokeMethod != null)
                target.InvokeMethod = FindCopiedMethod(target, source.InvokeMethod.Name);

            foreach (var method in source.OtherMethods)
            {
                var copiedMethod = FindCopiedMethod(target, method.Name);
                if (copiedMethod != null)
                    target.OtherMethods.Add(copiedMethod);
            }

            foreach (var ca in source.CustomAttributes)
            {
                target.CustomAttributes.Add(
                    new CustomAttribute((ICustomAttributeType)_mapper.Importer.Import(ca.Constructor)));
            }
        }

        private static MethodDef FindCopiedMethod(EventDef target, string name) =>
            target.DeclaringType.Methods.FirstOrDefault(m => m.Name == name && m.HasBody);
    }
}
