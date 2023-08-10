using HarmonyLib;
using Mono.Cecil.Cil;
using MoreInjuriesPatch.Patching.TranspilerUtils;
using Oof;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace MoreInjuriesPatch.Patching.HarmonyPatches;

using static MoreInjuriesPatchMod;

[HarmonyPatch(typeof(ShockComp), nameof(ShockComp.CompTended), typeof(float), typeof(float), typeof(int))]
public static class ShockComp_CompTended_Patch
{
    private static readonly MethodInfo _instanceGetBloodloss = typeof(ShockComp)
        .GetProperty(nameof(ShockComp.BloodLoss))
        .GetGetMethod();

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        const string CHECKPOINT_1_NULL_CHECK = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 1: null check injected";
        const string CHECKPOINT_2_IF_CONDITION_EXPANDED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 2: if-condition expanded";

        const string TARGET_NAME = $"{nameof(ShockComp)}.{nameof(ShockComp.CompTended)}()";

        Logger.Log($"Transpiling {TARGET_NAME} ...");

        TranspiledMethodBody transpiledMethodBody = TranspiledMethodBody.Empty()
            .DefineCheckpoint(CHECKPOINT_1_NULL_CHECK)
            .DefineCheckpoint(CHECKPOINT_2_IF_CONDITION_EXPANDED);

        long offset = 0;
        foreach(CodeInstruction instruction in instructions)
        {
            Logger.LogVerbose($"{instruction.opcode} {instruction.operand}");
            if (instruction.opcode == OpCodes.Brfalse_S)
            {
                transpiledMethodBody.Append(OpCodes.And);
                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_2_IF_CONDITION_EXPANDED))
                {
                    goto FAILURE;
                }
            }
            transpiledMethodBody.Append(instruction);
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo { DeclaringType.Name: nameof(HediffComp.CompTended) })
            {
                Logger.LogVerbose($"Found {OpCodes.Call} into base implementation at offset 0x{offset:x8}! Injecting patch...");
                // inject patch
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)                        // load "this" onto stack
                    .Append(OpCodes.Call, _instanceGetBloodloss)    // invoke this.BloodLoss, leave result on stack
                    .Append(OpCodes.Ldnull)                         // load null onto stack
                    .Append(OpCodes.Cgt_Un);                        // null check (void* > NULL), leave result on stack
                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_1_NULL_CHECK))
                {
                    goto FAILURE;
                }
            }
            offset += instruction.opcode.Size;
        }
        if (transpiledMethodBody.TryFlush(out IReadOnlyCollection<CodeInstruction>? patchedInstructions))
        {
            Logger.Log($"Successfully transpiled {TARGET_NAME}");
            if (Settings.enableVerboseLogging)
            {
                Logger.LogVerbose("Transpiled instructions:");
                foreach (CodeInstruction patchedInstruction in patchedInstructions!)
                {
                    Logger.LogVerbose($"{patchedInstruction.opcode} {patchedInstruction.operand}");
                }
            }
            return patchedInstructions!;
        }
    FAILURE:
        Logger.Error($"Failed to transpile {TARGET_NAME}! Reverting changes...");
        return instructions;
    }
}
