using Raylib_cs;
using RayMarcher.Framework;

namespace RayMarcher.Systems;

[FrameSystem(Phase.Init, order: -100)]
public class SharedApplicationState
{
    public SharedApplicationState(GameSettings settings)
    {
        int roundedHeight = (int)Math.Ceiling((double)settings.WindowHeight / 2) * 2;
        int roundedWidth = (int)Math.Ceiling((double)settings.WindowWidth / 2) * 2;
        WindowWidth = roundedWidth;
        WindowHeight = roundedHeight;
        MapWidth = settings.MapWidth;
        MapHeight = settings.MapHeight;
        MapDepth = settings.MapDepth;
        Seed = settings.Seed;
    }
    
    public int WindowWidth { get; }
    public int WindowHeight { get; }
    
    public int RenderWidth => WindowWidth / 4;
    public int RenderHeight => WindowHeight / 4;
    
    public int MapWidth, MapHeight, MapDepth;
    
    public int Seed { get; }

    [SetupMethod(Phase = SetupPhase.Core)]
    private void Init()
    {
        Raylib.InitWindow(WindowWidth, WindowHeight, "RayMarcher");
        Raylib.DisableCursor();
        Raylib.SetTargetFPS(30);
    }
}