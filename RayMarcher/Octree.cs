using System.Diagnostics;
using System.Numerics;

namespace RayMarcher;

[Flags]
internal enum Octant
{
    None = 0,

    PositiveX = 1 << 0, // bit 0: 1 = right half,  0 = left half
    PositiveY = 1 << 1, // bit 1: 1 = top half,    0 = bottom half
    PositiveZ = 1 << 2, // bit 2: 1 = front half,  0 = back half
    
    LeftBottomBack = None, // 0
    RightBottomBack = PositiveX, // 1
    LeftTopBack = PositiveY, // 2
    RightTopBack = PositiveX | PositiveY, // 3
    LeftBottomFront = PositiveZ, // 4
    RightBottomFront = PositiveX | PositiveZ, // 5
    LeftTopFront = PositiveY | PositiveZ, // 6
    RightTopFront = PositiveX | PositiveY | PositiveZ // 7
}

internal readonly struct VoxelMap(byte[] voxelMap, int width, int height, int depth)
{
    private readonly ushort _mapHeight = (ushort)Math.Clamp(height, ushort.MinValue, ushort.MaxValue);
    private readonly ushort _mapWidth = (ushort)Math.Clamp(width, ushort.MinValue, ushort.MaxValue);
    private readonly ushort _mapDepth = (ushort)Math.Clamp(depth, ushort.MinValue, ushort.MaxValue);

    public bool IsSolid(int x, int y, int z)
    {
        if (InBounds(x, y, z)) return this[x, y, z] != 0;
        return false;
    }

    private bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < _mapWidth &&
               y >= 0 && y < _mapHeight &&
               z >= 0 && z < _mapDepth;
    }

    private byte this[int x, int y, int z] => voxelMap[Index(x, y, z)];

    private int Index(int x, int y, int z)
    {
        return x + y * _mapWidth + z * _mapWidth * _mapHeight;
    }

    public (ushort height, ushort width, ushort depth) Dimensions => (_mapHeight, _mapWidth, _mapDepth);
}

[Flags]
internal enum NodeState
{
    None = 0,

    SolidVoxelFound = 1 << 0,
    EmptyVoxelFound = 1 << 1,

    Empty = EmptyVoxelFound,
    Solid = SolidVoxelFound,
    Mixed = SolidVoxelFound | EmptyVoxelFound,
    
    OutOfBounds = 4
}

internal class Node(Vector3 min, Vector3 max, NodeState state, Node[]? children, bool isLeaf)
{
    public Vector3 Min { get; set; } = min;
    public Vector3 Max { get; set; } = max;
    public Vector3 Center => (Min + Max) * 0.5f;

    public Node[]? Children { get; set; } = children;
    public NodeState State { get; set; } = state;

    public bool IsLeaf { get; set; } = isLeaf;

    public static Node BuildNode(Vector3 min, Vector3 max, VoxelMap voxelMap)
    {
        var state = NodeState.None;
        var (maxX, maxY, maxZ) = ((int)max.X, (int)max.Y, (int)max.Z);
        var (minX, minY, minZ) = ((int)min.X, (int)min.Y, (int)min.Z);

        CheckUniformity(ref state, minX, minY, minZ, maxX, maxY, maxZ, voxelMap.IsSolid);

        if (state == NodeState.Empty || state == NodeState.Solid)
            return new Node(min, max, state, null, true);

        var node = new Node(min, max, NodeState.Mixed, new Node[8], false);

        var center = node.Center;

        for (var child = 0; child < 8; child++)
        {
            var (childMin, childMax) = CalculateOctantBounds((Octant)child, min, center, max);
            var childNode = BuildNode(childMin, childMax, voxelMap);

            Debug.Assert(node.Children != null);
            node.Children[child] = childNode;
        }

        return node;
    }

    public Node FindNodeContaining(Vector3 point)
    {
        if (IsLeaf || Children == null)
        {
            return this;
        }
        var child = Children[(int)GetOctant(point)];
        return child.FindNodeContaining(point);
    }

    public static void Climb(ref NodeBuffer buffer, Vector3 point)
    {
        int index = buffer.ValidCount - 1;
        int startIndex = buffer.ValidCount - 1;
        Node current = buffer.Nodes[index];
        if (current.IsPointNodeWithinBounds(point))
        {
            return;
        }
        while (!current.IsPointNodeWithinBounds(point))
        {
            if (index == 0)
            {
                break;
            }

            index -= 1;
            current = buffer.Nodes[index];
        }

        int levelsClimbed = startIndex - index;
        buffer.ValidCount = index + 1;
    }

    public static Node Descend(ref NodeBuffer buffer, Vector3 point)
    {
        int index = buffer.ValidCount - 1;
        Node current = buffer.Nodes[index];
        while (!current.IsLeaf)
        {
            current = DescendToChildContaining(current, point);
            buffer.AppendNode(current);
        }
        return current;
    }

    public static Node DescendToChildContaining(Node node, Vector3 point)
    {
        Debug.Assert(node.Children != null);
        return node.Children[(int)Node.GetOctant(node, point)];
    }
    
    public static Octant GetOctant(Node node, Vector3 point)
    {
        var result = Octant.None;
        if (point.X >= node.Center.X) result |= Octant.PositiveX;
        if (point.Y >= node.Center.Y) result |= Octant.PositiveY;
        if (point.Z >= node.Center.Z) result |= Octant.PositiveZ;
        return result;
    }

    public bool IsPointNodeWithinBounds(Vector3 point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }
    

    private Octant GetOctant(Vector3 point)
    {
        var result = Octant.None;
        if (point.X >= Center.X) result |= Octant.PositiveX;
        if (point.Y >= Center.Y) result |= Octant.PositiveY;
        if (point.Z >= Center.Z) result |= Octant.PositiveZ;
        return result;
    }

    private static void CheckUniformity(ref NodeState state, int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
        Func<int, int, int, bool> isSolid)
    {
        for (var x = minX; x < maxX; x++)
        for (var y = minY; y < maxY; y++)
        for (var z = minZ; z < maxZ; z++)
        {
            if (state == NodeState.Mixed) return;

            if (isSolid(x, y, z))
                state |= NodeState.SolidVoxelFound;
            else
                state |= NodeState.EmptyVoxelFound;
        }
    }

    private static (Vector3 min, Vector3 max) CalculateOctantBounds(Octant octant, Vector3 min, Vector3 center,
        Vector3 max)
    {
        var octantMinimum = Vector3.Zero;
        var octantMaximum = Vector3.Zero;

        var hasPositiveX = (octant & Octant.PositiveX) != 0;
        var hasPositiveY = (octant & Octant.PositiveY) != 0;
        var hasPositiveZ = (octant & Octant.PositiveZ) != 0;

        octantMinimum.X = hasPositiveX ? center.X : min.X;
        octantMaximum.X = hasPositiveX ? max.X : center.X;
        octantMinimum.Y = hasPositiveY ? center.Y : min.Y;
        octantMaximum.Y = hasPositiveY ? max.Y : center.Y;
        octantMinimum.Z = hasPositiveZ ? center.Z : min.Z;
        octantMaximum.Z = hasPositiveZ ? max.Z : center.Z;

        return (octantMinimum, octantMaximum);
    }
    
    public static Node OutOfBounds => new Node(Vector3.Zero, Vector3.Zero, NodeState.OutOfBounds, null, true);
}

internal class Octree(Node root, int maxDepth)
{
    public Node Root { get; } = root;
    
    public int MaxDepth { get; } = maxDepth;

    public Node FindNodeContaining(Vector3 point)
    {
        if (point.X > Root.Max.X || point.Y > Root.Max.Y || point.Z > Root.Max.Z || point.X < Root.Min.X || point.Y < Root.Min.Y || point.Z < Root.Min.Z)
        {
            return Node.OutOfBounds;
        }
        return Root.FindNodeContaining(point);
    }

    public bool IsOutOfBounds(Vector3 point)
    {
        return point.X > Root.Max.X || point.Y > Root.Max.Y || point.Z > Root.Max.Z || point.X < Root.Min.X || point.Y < Root.Min.Y || point.Z < Root.Min.Z;
    }
    
    public static Octree Build(VoxelMap voxelMap)
    {
        uint longestDimension = Math.Max(voxelMap.Dimensions.height,
            Math.Max(voxelMap.Dimensions.width, voxelMap.Dimensions.depth));
        if (!BitOperations.IsPow2(longestDimension))
            longestDimension = BitOperations.RoundUpToPowerOf2(longestDimension);
        
        var maxDepth = BitOperations.TrailingZeroCount(longestDimension);
        var min = new Vector3(0, 0, 0);
        var max = new Vector3(longestDimension, longestDimension, longestDimension);
        var octree = new Octree(Node.BuildNode(min, max, voxelMap), maxDepth);
        return octree;
    }
}

internal struct NodeBuffer(int maxDepth)
{
    public Node[] Nodes { get; set; } = new Node[maxDepth + 1];

    public int ValidCount { get; set; } = 0;
    
    public int MaxDepth => maxDepth;

    public void AppendNode(Node node)
    {
        ValidCount += 1;
        Nodes[ValidCount - 1] = node;
        
    }

    public Node ClimbOneStep()
    {
        ValidCount -= 1;
        return Nodes[ValidCount];
    }
    
    public void Reset()
    {
        ValidCount = 1;
    }
}