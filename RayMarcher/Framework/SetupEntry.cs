namespace RayMarcher.Framework;

internal readonly record struct SetupEntry(
    Action Setup,
    SetupPhase Phase,
    int Order,
    string Name);