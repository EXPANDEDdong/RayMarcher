namespace RayMarcher.Framework;

internal readonly record struct FrameSystemBindings(
    List<TickEntry> Ticks,
    List<SetupEntry> Setups);