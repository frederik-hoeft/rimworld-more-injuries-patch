﻿using HarmonyLib;
using MoreInjuriesPatch.Patching;
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
        list.End();
        base.DoSettingsWindowContents(canvas);
    }

    public override string SettingsCategory() => "More Injuries Patch";
}
