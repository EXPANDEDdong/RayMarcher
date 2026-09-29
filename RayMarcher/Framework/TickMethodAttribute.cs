namespace RayMarcher.Framework;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class TickMethodAttribute : Attribute
{
    public Phase Phase { get; init; } = Phase.Undefined;
    public int Order { get; init; } = -999;
}