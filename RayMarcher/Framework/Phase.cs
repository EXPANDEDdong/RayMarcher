namespace RayMarcher.Framework;

public enum Phase
{
    Undefined = -900,
    Init = -100,
    Input = 0,
    Simulation = 100,
    PostSimulation = 200,
    Render = 300,
    Overlay = 400
}