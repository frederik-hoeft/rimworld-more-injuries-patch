namespace MoreInjuriesPatch.Patching.TranspilerUtils;

internal readonly record struct TranspilerCheckpoint(string Checkpoint, bool IsCompleted)
{
    public static TranspilerCheckpoint Create(string checkpoint) => new(checkpoint, false);
}