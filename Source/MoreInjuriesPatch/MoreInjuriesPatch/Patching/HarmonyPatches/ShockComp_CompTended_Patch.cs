using HarmonyLib;
using Oof;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace MoreInjuriesPatch.Patching.HarmonyPatches;

[HarmonyPatch(typeof(ShockComp), nameof(ShockComp.CompTended), typeof(float), typeof(float), typeof(int))]
public static class ShockComp_CompTended_Patch
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        Logger.Log($"Transpiling {nameof(ShockComp)}.{nameof(ShockComp.CompTended)} ...");
        List<CodeInstruction> methodInstructions = new(instructions);
        long offset = 0;
        for (int i = 0; i < methodInstructions.Count; i++)
        {
            CodeInstruction instruction = methodInstructions[i];
            Logger.LogVerbose($"{instruction.opcode} {instruction.operand}");
            if (instruction.opcode == OpCodes.Call)
            {
                Logger.LogVerbose($"Found {OpCodes.Call} at offset 0x{offset:x8}");
            }
            offset += instruction.opcode.Size;
        }
        return methodInstructions;
    }
}
