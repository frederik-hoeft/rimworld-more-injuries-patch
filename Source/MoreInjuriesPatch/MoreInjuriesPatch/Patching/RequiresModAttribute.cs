namespace MoreInjuriesPatch.Patching;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal class RequiresModAttribute : Attribute
{
    public string ModId { get; }

    public RequiresModAttribute(string modId) => ModId = modId;
}
