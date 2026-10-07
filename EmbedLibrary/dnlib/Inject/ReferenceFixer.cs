using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace dnlib.Inject
{
    /// <summary>
    /// After a type tree has been fully copied into the target module, rewrites any
    /// remaining references (method-body operands, method overrides and generic parameter
    /// constraints) so they point at imported/injected members instead of the original
    /// source-module members.
    /// </summary>
    internal sealed class ReferenceFixer
    {
        private readonly InjectMapper _mapper;

        public ReferenceFixer(InjectMapper mapper)
        {
            _mapper = mapper;
        }

        public void FixReferences(TypeDef target)
        {
            foreach (var method in target.Methods)
            {
                FixInstructionOperands(method);
                FixMethodOverrides(method);
            }

            foreach (var nestedType in target.NestedTypes)
                FixReferences(nestedType);
        }

        /// <summary>
        /// Re-resolves every generic parameter constraint's type. Now mostly a defensive
        /// no-op: <see cref="MemberCopier"/> already creates and correctly imports each
        /// constraint during the copy phase. Kept as a safety net in case a constraint
        /// references something outside the type's own tree that only became available
        /// later (e.g. a type injected in a previous, separate call).
        /// </summary>
        public void FixGenericParameters(TypeDef target)
        {
            foreach (var method in target.Methods)
            {
                if (method.HasGenericParameters)
                    FixGenericParamConstraints(method.GenericParameters);
            }

            if (target.HasGenericParameters)
                FixGenericParamConstraints(target.GenericParameters);

            foreach (var nestedType in target.NestedTypes)
                FixGenericParameters(nestedType);
        }

        private void FixInstructionOperands(MethodDef method)
        {
            if (!method.HasBody)
                return;

            foreach (var instruction in method.Body.Instructions)
            {
                switch (instruction.Operand)
                {
                    case ITypeDefOrRef typeRef:
                        instruction.Operand = _mapper.ImportType(typeRef);
                        break;

                    case IMemberRef memberRef:
                        try
                        {
                            instruction.Operand = _mapper.ImportMember(memberRef);
                        }
                        catch (Exception)
                        {
                            // If import fails, keep the original reference rather than
                            // failing the whole injection over a single operand.
                        }
                        break;
                }
            }
        }

        private void FixMethodOverrides(MethodDef method)
        {
            if (!method.HasOverrides)
                return;

            var newOverrides = new List<MethodOverride>();

            foreach (var methodOverride in method.Overrides)
            {
                try
                {
                    var importedDeclaration = _mapper.ImportMethod(methodOverride.MethodDeclaration);
                    if (importedDeclaration != null)
                        newOverrides.Add(new MethodOverride(method, importedDeclaration));
                }
                catch (Exception)
                {
                    // Skip overrides that can't be resolved in the target module.
                }
            }

            method.Overrides.Clear();
            foreach (var methodOverride in newOverrides)
                method.Overrides.Add(methodOverride);
        }

        private void FixGenericParamConstraints(IEnumerable<GenericParam> genericParameters)
        {
            foreach (var genericParam in genericParameters)
            {
                foreach (var constraint in genericParam.GenericParamConstraints)
                    constraint.Constraint = _mapper.ImportType(constraint.Constraint);
            }
        }
    }
}
