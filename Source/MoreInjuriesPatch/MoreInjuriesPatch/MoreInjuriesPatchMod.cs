using HarmonyLib;
using MoreInjuriesPatch.Patching;
using Oof;
using UnityEngine;
using Verse;

namespace MoreInjuriesPatch;

public class MoreInjuriesPatchMod : Mod
{
    public static MoreInjuriesPatchSettings Settings { get; private set; } = null!;

    public MoreInjuriesPatchMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<MoreInjuriesPatchSettings>();

        MoreInjuriesPatches.Apply(new Harmony("Th3Fr3d.MoreInjuriesPatched"));
    }

    public override void DoSettingsWindowContents(Rect canvas)
    {
        if (Settings is null)
        {
            Logger.Error("Settings was null!");
            throw new ArgumentNullException(nameof(Settings));
        }
        Listing_Standard list = new()
        {
            ColumnWidth = (canvas.width - 17) / 2
        };
        list.Begin(canvas);
        Text.Font = GameFont.Medium;
        list.Label("General settings");
        Text.Font = GameFont.Small;
        list.CheckboxLabeled("Is active", ref Settings.isActive);
        list.CheckboxLabeled("Enable logging", ref Settings.enableLogging);
        list.CheckboxLabeled("Enable verbose logging", ref Settings.enableVerboseLogging);
        list.GapLine();
        list.NewColumn();
        list.Label($"How much less likely it is for organ hypoxia to occur if hypovolemic shock is tended (as opposed to being left untreated): {Settings.tendedHypovolemicShockHypoxiaReductionFactor}");
        Settings.tendedHypovolemicShockHypoxiaReductionFactor = (float)Math.Round((double)list.Slider(Settings.tendedHypovolemicShockHypoxiaReductionFactor, 0.0f, 1f), 2);
        list.End();
        base.DoSettingsWindowContents(canvas);
    }

    public override string SettingsCategory() => "More Injuries Patch";
}
