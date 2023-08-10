using HarmonyLib;
using MoreInjuriesPatch.Patching.TranspilerUtils;
using Oof;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace MoreInjuriesPatch.Patching.HarmonyPatches;

using static MoreInjuriesPatchMod;

[HarmonyPatch(typeof(ShockComp), nameof(ShockComp.CompPostTick))]
public static class ShockComp_CompPostTick_Patch
{
    private static readonly FieldInfo _fixedNow = typeof(ShockComp)
        .GetField(nameof(ShockComp.fixedNow),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static readonly MethodInfo _instanceGetPastFixedPoint = typeof(ShockComp)
        .GetProperty(nameof(ShockComp.PastFixedPoint))
        .GetGetMethod();
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
     *   // CHECKPOINT_1_FIXED_POINT_CHECK
     *   if (!this.fixedNow)
     *      goto NOT_FIXED_NOW;
     *   if (!this.PastFixedPoint) 
     *      goto NOT_PAST_FIXED_POINT;
     *   goto SKIP_SEVERITY_INCREMENT;
     * NOT_PAST_FIXED_POINT:
     *   return;
     *   
     *   // old implementation
     *   if (this.fixedNow)
     *     return;
     *     
     *   NOT_FIXED_NOW:
     *   if (!this.PastFixedPoint)
     *   {
     *     this.parent.Severity = this.BloodLoss.Severity;
     *   }
     *   else
     *   {
     *     this.parent.Severity += 5E-05f;
     *     // CHECKPOINT_2_SKIP_SEVERITY_INCREMENT_LABEL
     *   SKIP_SEVERITY_INCREMENT:
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
     */
    /// <summary>
    /// Continue adding organ hypoxia as the body stabilizes until hypovolemic shock passes below 60% threshold (instead of instant fix)
    /// </summary>
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        const string CHECKPOINT_1_FIXED_POINT_CHECK = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 1: past-fixed-point check injected";
        const string CHECKPOINT_2_SKIP_SEVERITY_INCREMENT_LABEL = $"{nameof(ShockComp_CompPostTick_Patch)} transpiler checkpoint 2: SKIP_SEVERITY_INCREMENT label injected";

        const string TARGET_NAME = $"{nameof(ShockComp)}.{nameof(ShockComp.CompPostTick)}()";

        Logger.Log($"Transpiling {TARGET_NAME} ...");

        TranspiledMethodBody transpiledMethodBody = TranspiledMethodBody.Empty()
            .DefineCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK)
            .DefineCheckpoint(CHECKPOINT_2_SKIP_SEVERITY_INCREMENT_LABEL);

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

                // keep original implementation (required for label referencecs)
                for (; i < originalInstructions.Length; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    transpiledMethodBody.Append(originalInstruction);
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    if (originalInstructions[i].opcode == OpCodes.Brfalse)
                    {
                        break;
                    }
                }

                // label NOT_FIXED_NOW: (skipping past original implementation)
                transpiledMethodBody.Append(notFixedNowLabelContainer);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_1_FIXED_POINT_CHECK))
                {
                    goto FAILURE;
                }
                continue;
            }
            if (skipSeverityIncrementLabelContainer is not null && instruction.opcode == OpCodes.Add)
            {
                // skip to next nop
                for (i++; i < originalInstructions.Length && originalInstructions[i].opcode != OpCodes.Nop; i++)
                {
                    CodeInstruction originalInstruction = originalInstructions[i];
                    Logger.LogVerbose($"{originalInstruction.opcode} {originalInstruction.operand}");
                    transpiledMethodBody.Append(originalInstruction);
                }
                // skip past nop
                transpiledMethodBody.Append(originalInstructions[i]);
                Logger.LogVerbose($"{originalInstructions[i].opcode} {originalInstructions[i].operand}");
                // inject jump target/label SKIP_SEVERITY_INCREMENT
                transpiledMethodBody.Append(skipSeverityIncrementLabelContainer);

                if (!transpiledMethodBody.TryCompleteCheckpoint(CHECKPOINT_2_SKIP_SEVERITY_INCREMENT_LABEL))
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
}
