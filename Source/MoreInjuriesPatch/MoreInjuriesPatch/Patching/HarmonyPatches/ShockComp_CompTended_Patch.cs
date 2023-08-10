﻿using HarmonyLib;
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

    private static readonly MethodInfo _loggingHook = typeof(ShockComp_CompTended_Patch)
        .GetMethod(nameof(OnFixedNow_LoggingHook), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    /*
     * ORIGINAL IL LOGIC:
     * public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
     * {
     *     base.CompTended(quality, maxQuality, batchPosition);
     *     if ((quality < this.Props.BleedSeverityCurve.Evaluate(this.parent.Severity)) == false)
     *     {
     *         this.fixedNow = true;
     *     }
     *     return;
     * }
     * 
     * PATCHED IL LOGIC:
     * public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
     * {
     *     base.CompTended(quality, maxQuality, batchPosition);
     *     if (this.BloodLoss == null || (quality < this.Props.BleedSeverityCurve.Evaluate(this.parent.Severity)) == false)
     *     {
     *         this.fixedNow = true;
     *         ShockComp_CompTended_Patch.OnFixedNow_LoggingHook(this, quality);
     *     }
     *     return;
     * }
     */
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        const string CHECKPOINT_1_NULL_CHECK = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 1: null check injected";
        const string CHECKPOINT_2_IF_CONDITION_EXPANDED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 2: if-condition expanded";
        const string CHECKPOINT_3_INSTALLED_LOGGING_HOOK = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 3: logging hook installed!";

        const string TARGET_NAME = $"{nameof(ShockComp)}.{nameof(ShockComp.CompTended)}()";

        Logger.Log($"Transpiling {TARGET_NAME} ...");

        TranspiledMethodBody transpiledMethodBody = TranspiledMethodBody.Empty()
            .DefineCheckpoint(CHECKPOINT_1_NULL_CHECK)
            .DefineCheckpoint(CHECKPOINT_2_IF_CONDITION_EXPANDED)
            .DefineCheckpoint(CHECKPOINT_3_INSTALLED_LOGGING_HOOK);

        long offset = 0;
        foreach(CodeInstruction instruction in instructions)
        {
            Logger.LogVerbose($"{instruction.opcode} {instruction.operand}");
            if (instruction.opcode == OpCodes.Brfalse_S)
            {
                // OR our null check together with whatever is already on the stack
                transpiledMethodBody.Append(OpCodes.Or);
                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_2_IF_CONDITION_EXPANDED))
                {
                    goto FAILURE;
                }
            }
            transpiledMethodBody.Append(instruction);
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo { Name: nameof(HediffComp.CompTended), DeclaringType.Name: nameof(HediffComp) })
            {
                Logger.LogVerbose($"Found {OpCodes.Call} into base implementation of {TARGET_NAME} at offset 0x{offset:x8}! Applying patch...");
                // inject patch
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)                        // load "this" onto stack
                    .Append(OpCodes.Call, _instanceGetBloodloss)    // invoke this.BloodLoss, leave result on stack
                    .Append(OpCodes.Ldnull)                         // load null onto stack
                    .Append(OpCodes.Ceq);                           // null check (void* == NULL), leave result on stack
                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_1_NULL_CHECK))
                {
                    goto FAILURE;
                }
            }
            if (instruction.opcode == OpCodes.Stfld)
            {
                // install logging hook
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)
                    .Append(OpCodes.Ldarg_1)
                    .Append(OpCodes.Call, _loggingHook);
                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_3_INSTALLED_LOGGING_HOOK))
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

    private static void OnFixedNow_LoggingHook(ShockComp shockComp, float quality)
    {
        if (Settings.enableVerboseLogging)
        {
            double requiredQuality = shockComp.Props.BleedSeverityCurve.Evaluate(shockComp.parent.Severity);
            if ((double)quality >= requiredQuality)
            {
                Logger.Error($"{nameof(OnFixedNow_LoggingHook)} fired due to default behavior! Tending quality ({quality}) was good enough (>= {requiredQuality}) and hypovolemic shock *should* be fixed now :)");
            }
            else if (shockComp.BloodLoss is null)
            {
                Logger.Error($"{nameof(OnFixedNow_LoggingHook)} fired due to {nameof(ShockComp_CompTended_Patch)} (bloodloss was fixed)! Hypovolemic shock *should* be fixed now :)");
            }
            else
            {
                Logger.Error($"{nameof(OnFixedNow_LoggingHook)} fired for no reason :C {nameof(ShockComp_CompTended_Patch)} seems to be broken!");
            }
        }
    }
}
