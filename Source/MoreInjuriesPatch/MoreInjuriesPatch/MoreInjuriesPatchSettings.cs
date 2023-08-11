using Verse;

namespace MoreInjuriesPatch;

public class MoreInjuriesPatchSettings : ModSettings
{
    // general
    internal bool isActive = true;
    internal bool enableLogging = true;
    internal bool enableVerboseLogging = false;

    internal float tendedHypovolemicShockHypoxiaReductionFactor = 0.5f;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref isActive, nameof(isActive), true);
        Scribe_Values.Look(ref enableLogging, nameof(enableLogging), true);
        Scribe_Values.Look(ref enableVerboseLogging, nameof(enableVerboseLogging), true);

        Scribe_Values.Look(ref tendedHypovolemicShockHypoxiaReductionFactor, nameof(tendedHypovolemicShockHypoxiaReductionFactor), 0.5f);
    }
}
