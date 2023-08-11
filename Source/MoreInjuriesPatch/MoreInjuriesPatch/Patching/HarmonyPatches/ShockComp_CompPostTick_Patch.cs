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
     *   push to stack: !ShockComp_CompPostTick_Patch.IsTended_Hook(this) || ShockComp_CompPostTick_Patch.PushRandBool_Hook() 
     *   goto SKIP_SEVERITY_INCREMENT;
     * NOT_PAST_FIXED_POINT:
     *   return;
     *   NOT_FIXED_NOW:
     *   // CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED
     *   // CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_REMOVED
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
     *     ShockComp_CompPostTick_Patch.PushRandBool_Hook();
     *     goto HYPOXIA_GIVER;
     *   INCREMENT_BY_5
     *     // CHECKPOINT_4_IS_TENDED_HOOK_INSTALLED
     *     this.parent.Severity += 5E-05f;
     *     push true;
     *     // CHECKPOINT_5_ORIGINAL_INCREMENT_SKIPPED
     *   SKIP_SEVERITY_INCREMENT:
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
    /// 1. Continue adding organ hypoxia as the body stabilizes until hypovolemic shock passes below 60% threshold (instead of instant fix).
    /// 2. Automatically check for blood transfusion/bloodloss fix every 300 ticks
    /// 3. If tended and bloodloss still present => slow down severity increase
    /// 4. If tended but past 60% threshold => reduce organ hypoxia chance
    /// </summary>
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        const string CHECKPOINT_0_LD_ARG0_ENTRY_POINT_FOUND = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 0: ldarg.0 entry point found";
        const string CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 1: past-fixed-point check injected";
        const string CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_REMOVED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 2: original instructions skipped";
        const string CHECKPOINT_3_IS_TENDED_HOOK_INSTALLED = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 3: IsTended_Hook hook installed";
        const string CHECKPOINT_4_ORIGINAL_INCREMENT_SKIPPED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 4: original severity increment skipped";
        const string CHECKPOINT_5_HYPOXIA_GIVER_LABEL_INSERTED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 5: HYPOXIA_GIVER jump label inserted";
        const string CHECKPOINT_6_IF_CONDITION_EXPANDED = $"{nameof(ShockComp_CompTended_Patch)} transpiler checkpoint 6: if-condition expanded";

        const string TARGET_NAME = $"{nameof(ShockComp)}.{nameof(ShockComp.CompPostTick)}()";

        Logger.Log($"Transpiling {TARGET_NAME} ...");

        TranspiledMethodBody transpiledMethodBody = TranspiledMethodBody.Empty()
            .DefineCheckpoint(CHECKPOINT_0_LD_ARG0_ENTRY_POINT_FOUND)
            .DefineCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED)
            .DefineCheckpoint(CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_REMOVED)
            .DefineCheckpoint(CHECKPOINT_3_IS_TENDED_HOOK_INSTALLED)
            .DefineCheckpoint(CHECKPOINT_4_ORIGINAL_INCREMENT_SKIPPED)
            .DefineCheckpoint(CHECKPOINT_5_HYPOXIA_GIVER_LABEL_INSERTED)
            .DefineCheckpoint(CHECKPOINT_6_IF_CONDITION_EXPANDED);

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
                // skip nops and existing Ldarg_0 from old implementation.
                for (int j = i + 1; j < originalInstructions.Length; j++)
                {
                    CodeInstruction originalInstruction = originalInstructions[j];
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    transpiledMethodBody.Append(originalInstruction);
                    if (originalInstruction.opcode == OpCodes.Ldarg_0 && originalInstruction.labels.Count > 0)
                    {
                        if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_0_LD_ARG0_ENTRY_POINT_FOUND))
                        {
                            goto FAILURE;
                        }
                        break;
                    }
                }
                // we reuse existing Ldarg_0 from old implementation.
                // this is IMPORTANT because that old Ldarg_0 has a jump label which we want to hijack!
                // inject bloodloss evaluation hook
                transpiledMethodBody.Append(OpCodes.Call, _bloodlossEvalHook);

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
                     // load required stack values for if statement in CHECKPOINT_7_IF_CONDITION_EXPANDED
                    .Append(OpCodes.Ldarg_0)                            // this
                    .Append(OpCodes.Call, _isTendedHook)                // ShockComp_CompPostTick_Patch.IsTended_Hook(this)
                    .Append(OpCodes.Ldc_I4_0)                           
                    .Append(OpCodes.Ceq)                                // !ShockComp_CompPostTick_Patch.IsTended_Hook(this)
                    .Append(OpCodes.Ldarg_0)
                    .Append(OpCodes.Call, _pushRandBoolHook)
                    .Append(OpCodes.Or)                                 // || ShockComp_CompPostTick_Patch.PushRandBool_Hook()
                    .Append(OpCodes.Br, skipSeverityIncrementLabel)     // goto SKIP_SEVERITY_INCREMENT
                    .Append(notPastFixedPointLabelContainer)            // NOT_PAST_FIXED_POINT:
                    .Append(OpCodes.Ret)                                // return;
                    .Append(notFixedNowLabelContainer);                 // NOT_FIXED_NOW:

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK_INJECTED))
                {
                    goto FAILURE;
                }

                // discard original implementation
                for (; i < originalInstructions.Length; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    Logger.LogVerbose($"DISCARD: {originalInstruction.opcode} {originalInstruction.operand}");
                    if (originalInstructions[i].opcode == OpCodes.Brfalse)
                    {
                        if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_2_ORIGINAL_INSTRUCTIONS_REMOVED))
                        {
                            goto FAILURE;
                        }
                        break;
                    }
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

                Label hypoxiaGiverLabel = generator.DefineLabel();
                skipSeverityIncrementLabelContainer.labels.Add(hypoxiaGiverLabel);

                /*
                 *   if (!ShockComp_CompPostTick_Patch.IsTended_Hook(this))
                 *     goto INCREMENT_BY_5;
                 *   this.parent.Severity += 2.5E-05f;
                 *   PushRandBool_Hook();
                 *   goto HYPOXIA_GIVER;
                 * INCREMENT_BY_5:
                 *   this.parent.Severity += 5E-05f;
                 * SKIP_SEVERITY_INCREMENT:
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

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_3_IS_TENDED_HOOK_INSTALLED))
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
                        if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_4_ORIGINAL_INCREMENT_SKIPPED))
                        {
                            goto FAILURE;
                        }
                        break;
                    }
                }

                // inject labels and prepare CHECKPOINT_7_IF_CONDITION_EXPANDED
                transpiledMethodBody
                    .Append(OpCodes.Ldc_I4_1)                       // push true (always apply organ hypoxia), unless we're coming from:
                    .Append(skipSeverityIncrementLabelContainer);   // SKIP_SEVERITY_INCREMENT:,HYPOXIA_GIVER: 

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_5_HYPOXIA_GIVER_LABEL_INSERTED))
                {
                    goto FAILURE;
                }
            }
            if (instruction.opcode == OpCodes.Ldloc_3)
            {
                // combine (this.ticks >= 300) condition and _pushRandBoolHook/true
                transpiledMethodBody.Append(OpCodes.And);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_6_IF_CONDITION_EXPANDED))
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
    private static void EvaluateBloodlossState_Hook(ShockComp shockComp)
    {
        if (shockComp.ticks == 299 && !shockComp.fixedNow && ShockComp_CompTended_Patch.BloodlossFixed_Hook(shockComp))
        {
            shockComp.fixedNow = true;
            Logger.LogVerbose($"{nameof(ShockComp_CompPostTick_Patch)}.{nameof(EvaluateBloodlossState_Hook)}() fixed hypovolemic shock for {shockComp.Pawn?.Name}.");
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
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PushRandBool_Hook() => 
        !Rand.Chance(Settings.tendedHypovolemicShockHypoxiaReductionFactor);
}
