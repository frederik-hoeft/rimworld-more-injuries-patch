using HarmonyLib;
using MoreInjuriesPatch.Patching.TranspilerUtils;
using Oof;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Verse;

namespace MoreInjuriesPatch.Patching.HarmonyPatches;

using static MoreInjuriesPatchMod;

[HarmonyDebug]
[HarmonyPatch(typeof(ShockComp), nameof(ShockComp.CompPostTick))]
public static class ShockComp_CompPostTick_Patch
{
    private static readonly FieldInfo _fixedNow = typeof(ShockComp)
        .GetField(nameof(ShockComp.fixedNow),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static readonly MethodInfo _instanceGetPastFixedPoint = typeof(ShockComp)
        .GetProperty(nameof(ShockComp.PastFixedPoint))
        .GetGetMethod();

    private static readonly MethodInfo _bloodlossEvalHook = typeof(ShockComp_CompPostTick_Patch)
        .GetMethod(nameof(EvaluateBloodlossState_Hook), 
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);

    private static readonly MethodInfo _isTendedHook = typeof(ShockComp_CompPostTick_Patch)
        .GetMethod(nameof(IsTended_Hook),
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);

    private static readonly FieldInfo _parent = typeof(ShockComp)
        .GetField(nameof(ShockComp.parent), BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo _instanceGetSeverity = typeof(Hediff)
        .GetProperty(nameof(Hediff.Severity))
        .GetGetMethod();

    private static readonly MethodInfo _instanceSetSeverity = typeof(Hediff)
        .GetProperty(nameof(Hediff.Severity))
        .GetSetMethod();

    private static readonly MethodInfo _pushRandBoolHook = typeof(ShockComp_CompPostTick_Patch)
        .GetMethod(nameof(PushRandBool_Hook),
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
    /*
     * ORIGINAL IL LOGIC:
     * public override void CompPostTick(ref float severityAdjustment)
     * {
     *   ++this.ticks;
     *   base.CompPostTick(ref severityAdjustment);
     *   if (((this.BloodLoss != null ? 0 : (!this.PastFixedPoint ? 1 : 0)) | (this.fixedNow ? 1 : 0)) != 0)
     *     this.parent.Severity -= 2.5E-05f;
     *   if (this.fixedNow)
     *     return;
     *   if (!this.PastFixedPoint)
     *   {
     *     this.parent.Severity = this.BloodLoss.Severity;
     *   }
     *   else
     *   {
     *     this.parent.Severity += 5E-05f;
     *     if (this.ticks >= 300 && Rand.Chance(OofMod.settings.hypoxiaChance))
     *     {
     *       BodyPartRecord bodyPartRecord = this.InternalBps.Where<BodyPartRecord>((Func<BodyPartRecord, bool>) (x => (double) x.def.bleedRate > 0.0)).RandomElement<BodyPartRecord>();
     *       Hediff hediff = HediffMaker.MakeHediff(ShockDefOf.InternalSuffocation, this.parent.pawn, bodyPartRecord);
     *       hediff.Severity = Rand.Range(2f, 5f);
     *       this.parent.pawn.health.AddHediff(hediff, bodyPartRecord);
     *       this.ticks = 0;
     *     }
     *   }
     * }
     * 
     * PATCHED IL LOGIC:
     * public override void CompPostTick(ref float severityAdjustment)
     * {
     *   ++this.ticks;
     *   base.CompPostTick(ref severityAdjustment);
     *   if (((this.BloodLoss != null ? 0 : (!this.PastFixedPoint ? 1 : 0)) | (this.fixedNow ? 1 : 0)) != 0)
     *     this.parent.Severity -= 2.5E-05f;
     *   
     *   ShockComp_CompPostTick_Patch.EvaluateBloodlossState_Hook(this);
     *   if (!this.fixedNow)
     *      goto NOT_FIXED_NOW;
     *   if (!this.PastFixedPoint) 
     *      goto NOT_PAST_FIXED_POINT;
     *   goto SKIP_SEVERITY_INCREMENT;
     * NOT_PAST_FIXED_POINT:
     *   return;
     *   // CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED
     *   
     *   // old implementation
     *   if (this.fixedNow)
     *     return;
     *   // CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_SKIPPED
     *     
     *   NOT_FIXED_NOW:
     *   // CHECKPOINT_3_JUMP_LABEL_INSERTED
     *   
     *   if (!this.PastFixedPoint)
     *   {
     *     this.parent.Severity = this.BloodLoss.Severity;
     *   }
     *   else
     *   {
     *     if (!ShockComp_CompPostTick_Patch.IsTended_Hook(this))
     *       goto INCREMENT_BY_5;
     *     this.parent.Severity += 2.5E-05f;
     *     PushRandBool_Hook();
     *     goto HYPOXIA_GIVER;
     *   INCREMENT_BY_5
     *     // CHECKPOINT_4_IS_TENDED_HOOK_INSTALLED
     *     this.parent.Severity += 5E-05f;
     *     // CHECKPOINT_5_ORIGINAL_INCREMENT_SKIPPED
     *   SKIP_SEVERITY_INCREMENT:
     *     push true;
     *   HYPOXIA_GIVER:
     *     // CHECKPOINT_6_HYPOXIA_GIVER_LABEL_INSERTED
     *     // CHECKPOINT_7_IF_CONDITION_EXPANDED -----------+
     *                                                      |
     *                                                      v
     *     if (this.ticks >= 300 && <BOOLEAN_VALUE_ON_STACK> && Rand.Chance(OofMod.settings.hypoxiaChance)) 
     *     {
     *       BodyPartRecord bodyPartRecord = this.InternalBps.Where<BodyPartRecord>((Func<BodyPartRecord, bool>) (x => (double) x.def.bleedRate > 0.0)).RandomElement<BodyPartRecord>();
     *       Hediff hediff = HediffMaker.MakeHediff(ShockDefOf.InternalSuffocation, this.parent.pawn, bodyPartRecord);
     *       hediff.Severity = Rand.Range(2f, 5f);
     *       this.parent.pawn.health.AddHediff(hediff, bodyPartRecord);
     *       this.ticks = 0;
     *     }
     *   }
     * }
     */
    /// <summary>
    /// Continue adding organ hypoxia as the body stabilizes until hypovolemic shock passes below 60% threshold (instead of instant fix)
    /// </summary>
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        const string CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 1: past-fixed-point check injected";
        const string CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_SKIPPED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 2: original instructions skipped";
        const string CHECKPOINT_3_JUMP_LABEL_INSERTED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 3: NOT_FIXED_NOW jump label inserted";
        const string CHECKPOINT_4_IS_TENDED_HOOK_INSTALLED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 4: IsTended_Hook hook installed";
        const string CHECKPOINT_5_ORIGINAL_INCREMENT_SKIPPED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 5: original severity increment skipped";
        const string CHECKPOINT_6_HYPOXIA_GIVER_LABEL_INSERTED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 6: HYPOXIA_GIVER jump label inserted";
        const string CHECKPOINT_7_IF_CONDITION_EXPANDED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 7: if-condition expanded";

        const string TARGET_NAME = $"{nameof(ShockComp)}.{nameof(ShockComp.CompPostTick)}()";

        Logger.Log($"Transpiling {TARGET_NAME} ...");

        TranspiledMethodBody transpiledMethodBody = TranspiledMethodBody.Empty()
            .DefineCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED)
            .DefineCheckpoint(CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_SKIPPED)
            .DefineCheckpoint(CHECKPOINT_3_JUMP_LABEL_INSERTED)
            .DefineCheckpoint(CHECKPOINT_4_IS_TENDED_HOOK_INSTALLED)
            .DefineCheckpoint(CHECKPOINT_5_ORIGINAL_INCREMENT_SKIPPED)
            .DefineCheckpoint(CHECKPOINT_6_HYPOXIA_GIVER_LABEL_INSERTED)
            .DefineCheckpoint(CHECKPOINT_7_IF_CONDITION_EXPANDED);

        bool foundFirstSetSeverityCall = false;
        CodeInstruction[] originalInstructions = instructions.ToArray();
        CodeInstruction? skipSeverityIncrementLabelContainer = null;
        for (int i = 0; i < originalInstructions.Length; i++)
        {
            CodeInstruction instruction = originalInstructions[i];
            Logger.LogVerbose($"{instruction.opcode} {instruction.operand}");
            transpiledMethodBody.Append(instruction);
            if (!foundFirstSetSeverityCall && instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo { Name: $"set_{nameof(Hediff.Severity)}", DeclaringType.Name: nameof(Hediff) })
            {
                foundFirstSetSeverityCall = true;
                Logger.LogVerbose($"Found first {OpCodes.Callvirt} into Hediff.set_Severity! Applying patch...");
                // skip nops
                for (int j = i + 1; j < originalInstructions.Length; j++)
                {
                    CodeInstruction nop = originalInstructions[j];
                    if (nop.opcode == OpCodes.Nop)
                    {
                        Logger.LogVerbose($"{nop.opcode} {nop.operand}");
                        transpiledMethodBody.Append(nop);
                    }
                    else
                    {
                        i = j;
                        break;
                    }
                }

                // inject bloodloss evaluation hook
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)
                    .Append(OpCodes.Call, _bloodlossEvalHook);

                CodeInstruction notFixedNowLabelContainer = new(OpCodes.Nop);
                Label notFixedNowLabel = generator.DefineLabel();
                notFixedNowLabelContainer.labels.Add(notFixedNowLabel);

                CodeInstruction notPastFixedPointLabelContainer = new(OpCodes.Nop);
                Label notPastFixedPointLabel = generator.DefineLabel();
                notPastFixedPointLabelContainer.labels.Add(notPastFixedPointLabel);

                skipSeverityIncrementLabelContainer = new(OpCodes.Nop);
                Label skipSeverityIncrementLabel = generator.DefineLabel();
                skipSeverityIncrementLabelContainer.labels.Add(skipSeverityIncrementLabel);

                // inject patch
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)                            // push this
                    .Append(OpCodes.Ldfld, _fixedNow)                   // load this.fixedNow
                    .Append(OpCodes.Brfalse_S, notFixedNowLabel)        // if (!this.fixedNow) goto NOT_FIXED_NOW
                    .Append(OpCodes.Ldarg_0)                            // push this
                    .Append(OpCodes.Call, _instanceGetPastFixedPoint)   // load this.PastFixedPoint
                    .Append(OpCodes.Brfalse_S, notPastFixedPointLabel)  // if (!this.PastFixedPoint) goto NOT_PAST_FIXED_POINT
                    .Append(OpCodes.Br, skipSeverityIncrementLabel)     // goto SKIP_SEVERITY_INCREMENT
                    .Append(notPastFixedPointLabelContainer)            // NOT_PAST_FIXED_POINT:
                    .Append(OpCodes.Ret);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED))
                {
                    goto FAILURE;
                }

                // keep original implementation (required for label referencecs)
                for (; i < originalInstructions.Length; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    transpiledMethodBody.Append(originalInstruction);
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    if (originalInstructions[i].opcode == OpCodes.Brfalse)
                    {
                        if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_SKIPPED))
                        {
                            goto FAILURE;
                        }
                        break;
                    }
                }

                // label NOT_FIXED_NOW: (skipping past original implementation)
                transpiledMethodBody.Append(notFixedNowLabelContainer);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_3_JUMP_LABEL_INSERTED))
                {
                    goto FAILURE;
                }
                continue;
            }
            if (skipSeverityIncrementLabelContainer is not null && instruction.opcode == OpCodes.Br)
            {
                // skip past next nop
                for (i++; i < originalInstructions.Length && originalInstructions[i].opcode != OpCodes.Nop; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    transpiledMethodBody.Append(originalInstruction);
                }
                transpiledMethodBody.Append(originalInstructions[i]);
                Logger.LogVerbose($"{originalInstructions[i].opcode} {originalInstructions[i].operand}");

                CodeInstruction incrementBy5LabelContainer = new(OpCodes.Nop);
                Label incrementBy5Label = generator.DefineLabel();
                incrementBy5LabelContainer.labels.Add(incrementBy5Label);

                CodeInstruction hypoxiaGiverLabelContainer = new(OpCodes.Nop);
                Label hypoxiaGiverLabel = generator.DefineLabel();
                incrementBy5LabelContainer.labels.Add(incrementBy5Label);

                /*
                 *   if (!ShockComp_CompPostTick_Patch.IsTended_Hook(this))
                 *     goto INCREMENT_BY_5;
                 *   this.parent.Severity += 2.5E-05f;
                 *   PushRandBool_Hook();
                 *   goto HYPOXIA_GIVER;
                 * INCREMENT_BY_5:
                 *   this.parent.Severity += 5E-05f;
                 * SKIP_SEVERITY_INCREMENT:
                 *   push true;
                 * HYPOXIA_GIVER:
                 */

                // inject patch
                transpiledMethodBody
                    .Append(OpCodes.Ldarg_0)                        // this
                    .Append(OpCodes.Call, _isTendedHook)            // ShockComp_CompPostTick_Patch.IsTended_Hook(this)
                    .Append(OpCodes.Brfalse_S, incrementBy5Label)   // if (!ShockComp_CompPostTick_Patch.IsTended_Hook(this)) goto INCREMENT_BY_5;
                    .Append(OpCodes.Ldarg_0)                        // this
                    .Append(OpCodes.Ldfld, _parent)                 // this.parent
                    .Append(OpCodes.Dup)                            // this.parent this.parent
                    .Append(OpCodes.Callvirt, _instanceGetSeverity) // this.parent this.parent.Severity
                    .Append(OpCodes.Ldc_R4, 2.5e-05f)               // this.parent this.parent.Severity 2.5e-05f
                    .Append(OpCodes.Add)                            // this.parent this.parent.Severity + 2.5e-05f
                    .Append(OpCodes.Callvirt, _instanceSetSeverity) // this.parent.Severity = this.parent.Severity + 2.5e-05f
                    .Append(OpCodes.Call, _pushRandBoolHook)        // ShockComp_CompPostTick_Patch.PushRandBool_Hook()
                    .Append(OpCodes.Br_S, hypoxiaGiverLabel)        // goto HYPOXIA_GIVER;
                    .Append(incrementBy5LabelContainer);            // INCREMENT_BY_5:

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_4_IS_TENDED_HOOK_INSTALLED))
                {
                    goto FAILURE;
                }
                // preserve original increment-by-5e-05f implementation...
                // skip past next callvirt instance void ['Assembly-CSharp']Verse.Hediff::set_Severity(float32)
                for (i++; i < originalInstructions.Length; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    transpiledMethodBody.Append(originalInstruction);
                    if (originalInstructions[i].opcode == OpCodes.Callvirt && originalInstructions[i].operand is MethodInfo { Name: $"set_{nameof(Hediff.Severity)}" })
                    {
                        if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_5_ORIGINAL_INCREMENT_SKIPPED))
                        {
                            goto FAILURE;
                        }
                        break;
                    }
                }

                // inject labels and prepare CHECKPOINT_7_IF_CONDITION_EXPANDED
                transpiledMethodBody
                    .Append(skipSeverityIncrementLabelContainer)    // SKIP_SEVERITY_INCREMENT:
                    .Append(OpCodes.Ldc_I4_1)                       // push true;
                    .Append(hypoxiaGiverLabelContainer);            // HYPOXIA_GIVER:

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_6_HYPOXIA_GIVER_LABEL_INSERTED))
                {
                    goto FAILURE;
                }
            }
            if (instruction.opcode == OpCodes.Ldloc_3)
            {
                // combine (this.ticks >= 300) condition and _pushRandBoolHook/true
                transpiledMethodBody.Append(OpCodes.And);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_7_IF_CONDITION_EXPANDED))
                {
                    goto FAILURE;
                }
            }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EvaluateBloodlossState_Hook(ShockComp comp)
    {
        if (comp.ticks >= 300 && !comp.fixedNow && comp.BloodLoss?.Severity is null or < 0.15f)
        {
            comp.fixedNow = true;
            Logger.LogVerbose($"{nameof(ShockComp_CompPostTick_Patch)}.{nameof(ShockComp_CompPostTick_Patch.EvaluateBloodlossState_Hook)}() fixed hypovolemic shock for {comp.Pawn?.Name}.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTended_Hook(ShockComp comp)
    {
        List<HediffComp> comps = comp.parent.comps;
        for (int i = 0; i < comps.Count; i++)
        {
            if (comps[i] is HediffComp_TendDuration duration)
            {
                return !duration.AllowTend;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PushRandBool_Hook() => 
        !Rand.Chance(Settings.tendedHypovolemicShockHypoxiaReductionFactor);
}
