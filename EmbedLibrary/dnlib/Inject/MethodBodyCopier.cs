using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace dnlib.Inject
{
    /// <summary>
    /// Deep-copies a method body (locals, instructions and exception handlers) from a
    /// source <see cref="MethodDef"/> into its already-cloned target counterpart, importing
    /// every type/method/field referenced by an instruction operand along the way.
    /// </summary>
    internal sealed class MethodBodyCopier
    {
        private readonly InjectMapper _mapper;

        public MethodBodyCopier(InjectMapper mapper)
        {
            _mapper = mapper;
        }

        public void Copy(MethodDef source, MethodDef target)
        {
            if (!source.HasBody)
                return;

            target.Body = new CilBody(
                source.Body.InitLocals,
                new List<Instruction>(),
                new List<ExceptionHandler>(),
                new List<Local>())
            {
                MaxStack = source.Body.MaxStack
            };

            // Maps source locals/instructions to their freshly-created counterparts so that
            // branch targets, leave targets and exception-handler ranges can be rewritten
            // once every instruction has been created (see RewriteOperandReferences).
            var bodyMap = new Dictionary<object, object>();

            CopyLocals(source, target, bodyMap);
            CopyInstructions(source, target, bodyMap);
            RewriteOperandReferences(target, bodyMap);
            CopyExceptionHandlers(source, target, bodyMap);

            target.Body.SimplifyMacros(target.Parameters);
        }

        private void CopyLocals(MethodDef source, MethodDef target, IDictionary<object, object> bodyMap)
        {
            foreach (Local local in source.Body.Variables)
            {
                var newLocal = new Local(_mapper.ImportTypeSig(local.Type));
                target.Body.Variables.Add(newLocal);
                bodyMap[local] = newLocal;
            }
        }

        private void CopyInstructions(MethodDef source, MethodDef target, IDictionary<object, object> bodyMap)
        {
            foreach (Instruction instr in source.Body.Instructions)
            {
                var newInstr = new Instruction(instr.OpCode, instr.Operand);

                switch (newInstr.Operand)
                {
                    case IType typeOperand:
                        newInstr.Operand = _mapper.ImportType(typeOperand);
                        break;
                    case IMethod methodOperand:
                        newInstr.Operand = _mapper.ImportMethod(methodOperand);
                        break;
                    case IField fieldOperand:
                        newInstr.Operand = _mapper.ImportField(fieldOperand);
                        break;
                }

                target.Body.Instructions.Add(newInstr);
                bodyMap[instr] = newInstr;
            }
        }

        private static void RewriteOperandReferences(MethodDef target, IDictionary<object, object> bodyMap)
        {
            // A second pass is required because branch/leave/switch operands reference other
            // instructions (or locals) by identity, which only exist once the first pass
            // above has finished creating every instruction.
            foreach (Instruction instr in target.Body.Instructions)
            {
                if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
                {
                    instr.Operand = bodyMap[instr.Operand];
                }
                else if (instr.Operand is Instruction[] switchTargets)
                {
                    instr.Operand = switchTargets.Select(t => (Instruction)bodyMap[t]).ToArray();
                }
            }
        }

        private void CopyExceptionHandlers(MethodDef source, MethodDef target, IDictionary<object, object> bodyMap)
        {
            foreach (ExceptionHandler eh in source.Body.ExceptionHandlers)
            {
                target.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
                {
                    CatchType = eh.CatchType == null ? null : _mapper.ImportType(eh.CatchType),
                    TryStart = (Instruction)bodyMap[eh.TryStart],
                    TryEnd = (Instruction)bodyMap[eh.TryEnd],
                    HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
                    HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
                    FilterStart = eh.FilterStart == null ? null : (Instruction)bodyMap[eh.FilterStart]
                });
            }
        }
    }
}
