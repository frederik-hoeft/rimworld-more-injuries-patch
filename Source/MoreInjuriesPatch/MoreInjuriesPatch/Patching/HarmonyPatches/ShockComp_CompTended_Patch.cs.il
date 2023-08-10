.method public hidebysig virtual instance void
    CompTended(
      float32 quality,
      float32 maxQuality,
      [opt] int32 batchPosition
    ) cil managed
  {
    .param [3] = int32(0) // 0x00000000
    .maxstack 4
    .locals init (
      [0] bool V_0
    )

    IL_0000: nop

    // [26 7 - 26 58]
    IL_0001: ldarg.0      // this
    IL_0002: ldarg.1      // quality
    IL_0003: ldarg.2      // maxQuality
    IL_0004: ldarg.3      // batchPosition
    IL_0005: call         instance void ['Assembly-CSharp']Verse.HediffComp::CompTended(float32, float32, int32)

//  INJECT CHECKPOINT_1_NULL_CHECK HERE!

    IL_000a: nop

    // [27 7 - 27 100]
    IL_000b: ldarg.1      // quality
    IL_000c: ldarg.0      // this
    IL_000d: call         instance class Oof.ShockCompProps Oof.ShockComp::get_Props()
    IL_0012: ldfld        class ['Assembly-CSharp']Verse.SimpleCurve Oof.ShockCompProps::BleedSeverityCurve
    IL_0017: ldarg.0      // this
    IL_0018: ldfld        class ['Assembly-CSharp']Verse.HediffWithComps ['Assembly-CSharp']Verse.HediffComp::parent
    IL_001d: callvirt     instance float32 ['Assembly-CSharp']Verse.Hediff::get_Severity()
    IL_0022: callvirt     instance float32 ['Assembly-CSharp']Verse.SimpleCurve::Evaluate(float32)
    IL_0027: clt.un
    IL_0029: ldc.i4.0
    IL_002a: ceq
    IL_002c: stloc.0      // V_0

    IL_002d: ldloc.0      // V_0

//  INJECT CHECKPOINT_2_IF_CONDITION_EXPANDED HERE!

    IL_002e: brfalse.s    IL_0039
    IL_0030: nop

    // [29 7 - 29 27]
    IL_0031: ldarg.0      // this
    IL_0032: ldc.i4.1
    IL_0033: stfld        bool Oof.ShockComp::fixedNow

//  INJECT CHECKPOINT_3_INSTALLED_LOGGING_HOOK HERE!

    IL_0038: nop

    IL_0039: ret

  } // end of method ShockComp::CompTended