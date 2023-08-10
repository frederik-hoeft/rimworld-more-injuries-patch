using HarmonyLib;
using System.Linq;
using System.Reflection;
using Verse;

namespace MoreInjuriesPatch.Patching;

internal static class MoreInjuriesPatches
{
    public static void Apply(Harmony harmony)
    {
        Logger.LogAlways($"applying patches...");
        Type[] harmonyPatches = typeof(MoreInjuriesPatches).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed // static class <==> class && sealed && abstract
                && t.TryGetAttribute<HarmonyPatch>(out _))
            .ToArray();

        int patchCount = 0;
        foreach (Type harmonyPatch in harmonyPatches)
        {
            if (harmonyPatch.TryGetAttribute(out RequiresModAttribute? dependency) 
                && !LoadedModManager.RunningMods.Any(mod => 
                    mod.PackageIdPlayerFacing.Equals(dependency!.ModId, StringComparison.InvariantCultureIgnoreCase)))
            {
                Logger.LogAlways($"skipping {harmonyPatch.Name} due to missing dependency: '{dependency!.ModId}'");
                continue;
            }
            harmony.CreateClassProcessor(harmonyPatch).Patch();
            Logger.LogAlways($"applied {harmonyPatch.Name}{(dependency is not null ? $" because {dependency.ModId} was detected" : string.Empty)}");
            patchCount++;
        }
        Logger.LogAlways($"applied {patchCount} patches!");
    }

    // .NET Framework doesn't support null-state static analysis :C
    // (also: why is RimWorld still using .NET Framework? it's not 2016 anymore :P)
    private static bool TryGetAttribute<TAttribute>(this Type type, /*[NotNullWhen(true)]*/ out TAttribute? attribute) 
        where TAttribute : Attribute
    {
        attribute = type.GetCustomAttribute<TAttribute>();
        return attribute is not null;
    }
}
