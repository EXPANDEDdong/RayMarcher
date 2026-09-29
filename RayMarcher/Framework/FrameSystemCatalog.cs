namespace RayMarcher.Framework;

public sealed class FrameSystemCatalog(IReadOnlyList<Type> types)
{
    public IReadOnlyList<Type> Types { get; } = types;
}