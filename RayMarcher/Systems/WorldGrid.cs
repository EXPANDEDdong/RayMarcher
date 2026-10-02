using NoiseDotNet;
using RayMarcher.Framework;
using RayMarcher.Utils;

namespace RayMarcher.Systems;

public interface IWorldGrid
{
    Octree Octree { get; }
    int Index(int x, int y, int z);
    bool IsSolid(int x, int y, int z);
    bool InBounds(int x, int y, int z);
}

[FrameSystem(Phase.Init, -90)]
public class WorldGrid(SharedApplicationState state) : IWorldGrid
{
    private byte[] voxels;
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    public int MapDepth { get; private set; }

    public Octree Octree { get; private set; }
    
    public byte[] Voxels => voxels;

    public int Index(int x, int y, int z)
    {
        return x + y * MapWidth + z * MapWidth * MapHeight;
    }

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < MapWidth &&
               y >= 0 && y < MapHeight &&
               z >= 0 && z < MapDepth;
    }

    public bool IsSolid(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return false;
        return voxels[Index(x, y, z)] != 0;
    }

    [SetupMethod(Phase = SetupPhase.World, Order = -10)]
    public void Init()
    {
        MapWidth = state.MapWidth;
        MapHeight = state.MapHeight;
        MapDepth = state.MapDepth;
        voxels = new byte[MapWidth * MapHeight * MapDepth];
    }

    [SetupMethod(Phase = SetupPhase.World, Order = 0)]
    public void GenerateMap()
    {
        var columnCount = MapWidth * MapDepth;
        var xCoords = new float[columnCount];
        var zCoords = new float[columnCount];
        var noise = new float[columnCount];

        var idx = 0;
        for (var z = 0; z < MapDepth; z++)
        for (var x = 0; x < MapWidth; x++)
        {
            xCoords[idx] = x;
            zCoords[idx] = z;
            idx++;
        }

        const float noiseFrequency = 0.09f;

        var settings = new NoiseSettings(noiseFrequency, noiseFrequency, state.Seed);
        Noise.GradientNoise2D(xCoords, zCoords, noise, settings);

        var baseHeight = MapHeight / 4;
        var heightVariance = MapHeight / 4;

        idx = 0;
        for (var z = 0; z < MapDepth; z++)
        for (var x = 0; x < MapWidth; x++)
        {
            var groundHeight = baseHeight + (int)MathF.Round(noise[idx] * heightVariance);
            groundHeight = Math.Clamp(groundHeight, 1, MapHeight - 2);
            idx++;

            for (var y = 0; y < MapHeight; y++)
            {
                var solid = y <= groundHeight;
                var surface = y == groundHeight;
                var underGround = y < (groundHeight - 5);
                
                int material = 0;
                
                if (surface)
                {
                    material = 2;
                } else if (underGround)
                {
                    material = 3;
                }
                else
                {
                    material = 1;
                }

                if (solid) voxels[Index(x, y, z)] = (byte)material;
            }
        }
    }

    [SetupMethod(Phase = SetupPhase.World, Order = 20)]
    public void GenerateOctree()
    {
        var voxelMap = new VoxelMap(voxels, MapWidth, MapHeight, MapDepth);
        Octree = Octree.Create(ref voxelMap);
    }
}