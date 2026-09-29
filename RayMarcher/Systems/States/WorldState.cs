using RayMarcher.Utils;

namespace RayMarcher.Systems.States;

public class WorldState : IWorldStateWriter, IWorldStateReader
{
    public Octree Octree { get; set; }

    public WorldStateCopy GetWorldState()
    {
        return new WorldStateCopy(Octree);
    }

    public void PushOctree(Octree octree)
    {
        Octree = octree;
    }
}

public interface IWorldStateWriter
{
    void PushOctree(Octree octree);
}

public interface IWorldStateReader
{
    WorldStateCopy GetWorldState();
}

public record struct WorldStateCopy(Octree Octree);