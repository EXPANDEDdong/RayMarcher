namespace RayMarcher.Framework;

[AttributeUsage(AttributeTargets.Class,  AllowMultiple = false, Inherited = false)]
public sealed class FrameSystemAttribute(Phase phase, int order) : Attribute
{
    public Phase Phase { get; } = phase;
    public int Order { get; } = order;
}