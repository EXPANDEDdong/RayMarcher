namespace RayMarcher.Framework;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SetupMethodAttribute : Attribute
{
    public SetupPhase Phase { get; init; } = SetupPhase.Services;
    public int Order { get; init; } = 0;
}