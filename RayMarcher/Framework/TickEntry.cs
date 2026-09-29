namespace RayMarcher.Framework;

internal readonly record struct TickEntry(
    Action<float> Tick,
    Phase Phase,
    int Order,
    string Name);