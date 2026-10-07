using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// Fills in the details of definitions that have already been cloned into the target
    /// module and registered in <see cref="InjectMapper.DnlibDefMaps"/>: base type,
    /// interfaces, signatures, custom attributes, security declarations, constants,
    /// marshaling info, generic parameter constraints, parameter metadata, field initial
    /// data (RVA) and method bodies.
    /// </summary>
    internal sealed class MemberCopier
    {
        private readonly InjectMapper _mapper;
        private readonly MethodBodyCopier _bodyCopier;
        private readonly AttributeCopier _attributeCopier;

        public MemberCopier(InjectMapper mapper)
        {
            _mapper = mapper;
            _bodyCopier = new MethodBodyCopier(mapper);
            _attributeCopier = new AttributeCopier(mapper);
        }

        /// <summary>
        /// Copies every member of <paramref name="source"/> (and, if
        /// <paramref name="copyTypeAttributes"/> is true, the type itself) into its injected
        /// counterpart. Deliberately does NOT recurse into nested types - see
        /// <see cref="TypeInitializer.CopyTree"/>, which calls this once per type.
        /// </summary>
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

            foreach (var ca in source.CustomAttributes)
                target.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

            if (source.HasDeclSecurities)
            {
                foreach (var declSecurity in source.DeclSecurities)
                    target.DeclSecurities.Add(_attributeCopier.CopyDeclSecurity(declSecurity));
            }

            CopyGenericParameterDetails(source.GenericParameters, target.GenericParameters);
        }

        private void CopyMethodDef(MethodDef source)
        {
            var target = (MethodDef)_mapper.DnlibDefMaps[source];

            target.Signature = _mapper.ImportCallingConventionSig(source.Signature);

            if (source.ImplMap != null)
            {
                target.ImplMap = new ImplMapUser(
                    new ModuleRefUser(_mapper.TargetModule, source.ImplMap.Module.Name),
                    source.ImplMap.Name,
                    source.ImplMap.Attributes);
            }

            foreach (CustomAttribute ca in source.CustomAttributes)
                target.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

            if (source.HasDeclSecurities)
            {
                foreach (var declSecurity in source.DeclSecurities)
                    target.DeclSecurities.Add(_attributeCopier.CopyDeclSecurity(declSecurity));
            }

            CopyParamDefs(source, target);
            CopyGenericParameterDetails(source.GenericParameters, target.GenericParameters);

            _bodyCopier.Copy(source, target);
        }

        /// <summary>
        /// Copies <see cref="MethodDef.ParamDefs"/> - parameter names, attributes
        /// (<c>[In]</c>/<c>[Out]</c>/<c>[Optional]</c>), default values and marshaling info.
        /// <see cref="ParameterList"/> (<c>MethodDef.Parameters</c>) is derived from the
        /// signature plus this list, so without it parameters keep their type but lose their
        /// name and everything else.
        /// </summary>
        private void CopyParamDefs(MethodDef source, MethodDef target)
        {
            foreach (ParamDef sourceParam in source.ParamDefs)
            {
                var targetParam = new ParamDefUser(sourceParam.Name, sourceParam.Sequence, sourceParam.Attributes);

                if (sourceParam.Constant != null)
                    targetParam.Constant = CopyConstant(sourceParam.Constant);

                if (sourceParam.MarshalType != null)
                    targetParam.MarshalType = sourceParam.MarshalType;

                foreach (var ca in sourceParam.CustomAttributes)
                    targetParam.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

                target.ParamDefs.Add(targetParam);
            }

            target.Parameters.UpdateParameterTypes();
        }

        private void CopyFieldDef(FieldDef source)
        {
            var target = (FieldDef)_mapper.DnlibDefMaps[source];

            target.Signature = _mapper.Importer.Import(source.Signature);

            foreach (var ca in source.CustomAttributes)
                target.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

            // Constant value: this is what makes enum members and const fields actually
            // carry their value - without it every enum member/const field becomes 0/null.
            if (source.Constant != null)
                target.Constant = CopyConstant(source.Constant);

            // Marshaling info ([MarshalAs], used for P/Invoke interop). MarshalType
            // descriptors don't hold module-bound metadata tokens, so they can be reused
            // directly without going through the importer.
            if (source.MarshalType != null)
                target.MarshalType = source.MarshalType;

            // Explicit struct layout ([FieldOffset]).
            if (source.FieldOffset.HasValue)
                target.FieldOffset = source.FieldOffset;

            // Initial data stored at the field's RVA - e.g. the backing data for large
            // compiler-generated array initializers. Without this the field exists but its
            // embedded bytes are silently dropped.
            if ((source.Attributes & FieldAttributes.HasFieldRVA) != 0 && source.InitialValue != null)
                target.InitialValue = source.InitialValue;
        }

        private void CopyPropertyDef(PropertyDef source)
        {
            var target = (PropertyDef)_mapper.DnlibDefMaps[source];

            if (source.SetMethod != null && source.SetMethod.HasBody)
                target.SetMethod = target.DeclaringType.Methods.First(m => m.Name == $"set_{source.Name}");

            if (source.GetMethod != null && source.GetMethod.HasBody)
                target.GetMethod = target.DeclaringType.Methods.First(m => m.Name == $"get_{source.Name}");

            foreach (var ca in source.CustomAttributes)
                target.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

            if (source.Constant != null)
                target.Constant = CopyConstant(source.Constant);
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
                target.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));
        }

        /// <summary>
        /// Copies custom attributes on each generic parameter, and - crucially - creates the
        /// generic parameter *constraints* themselves (e.g. <c>where T : IComparable</c>).
        /// </summary>
        /// <remarks>
        /// The original implementation never created constraints on the cloned generic
        /// parameters at all: <c>CloneGenericParam</c> only copied number/flags/name, and the
        /// later "fix" pass iterated the target's (always empty) <c>GenericParamConstraints</c>
        /// collection, so it had nothing to fix. Every generic constraint silently
        /// disappeared after injection. This pairs each source generic parameter with its
        /// already-cloned target counterpart by index (both lists are built in the same
        /// order) and creates a matching, properly-imported constraint for each one.
        /// </remarks>
        private void CopyGenericParameterDetails(IList<GenericParam> sourceParams, IList<GenericParam> targetParams)
        {
            for (int i = 0; i < sourceParams.Count; i++)
            {
                var sourceParam = sourceParams[i];
                var targetParam = targetParams[i];

                foreach (var ca in sourceParam.CustomAttributes)
                    targetParam.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

                foreach (var constraint in sourceParam.GenericParamConstraints)
                {
                    var newConstraint = new GenericParamConstraintUser(_mapper.ImportType(constraint.Constraint));

                    foreach (var ca in constraint.CustomAttributes)
                        newConstraint.CustomAttributes.Add(_attributeCopier.CopyCustomAttribute(ca));

                    targetParam.GenericParamConstraints.Add(newConstraint);
                }
            }
        }

        private static Constant CopyConstant(Constant source) =>
            new ConstantUser(source.Value, source.Type);

        private static MethodDef FindCopiedMethod(EventDef target, string name) =>
            target.DeclaringType.Methods.FirstOrDefault(m => m.Name == name && m.HasBody);
    }
}
