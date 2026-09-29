using System.Numerics;
using System.Runtime.InteropServices;

namespace RayMarcher.Utils;

[Flags]
public enum Octant
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

public readonly struct VoxelMap(byte[] voxelMap, int width, int height, int depth)
{
    private readonly ushort _mapHeight = (ushort)Math.Clamp(height, ushort.MinValue, ushort.MaxValue);
    private readonly ushort _mapWidth = (ushort)Math.Clamp(width, ushort.MinValue, ushort.MaxValue);
    private readonly ushort _mapDepth = (ushort)Math.Clamp(depth, ushort.MinValue, ushort.MaxValue);

    public bool IsSolid(int x, int y, int z)
    {
        if (InBounds(x, y, z)) return this[x, y, z] != 0;
        return false;
    }
    
    public byte TextureFor(int x, int y, int z) => this[x, y, z];

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
public enum NodeState : byte
{
    None = 0,

    SolidVoxelFound = 1 << 0,
    EmptyVoxelFound = 1 << 1,

    Empty = EmptyVoxelFound,
    Solid = SolidVoxelFound,
    Mixed = SolidVoxelFound | EmptyVoxelFound,
    
    OutOfBounds = 4
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Node(uint childStartIndex, NodeState state = NodeState.Empty, byte materialId = 0, ushort extraData = 0)
{
    public uint ChildStartIndex { get; } = childStartIndex; // 4 bytes
    public NodeState State { get; } = state; // 1 byte
    public byte MaterialId { get; } = materialId; // 1 byte
    public ushort ExtraData { get; } = extraData; // 2 bytes
    
    public static Node Empty => new(0, NodeState.None);
    
    public Octant GetOctant((int x, int y, int z) min, uint sideLength, Vector3 point)
    {
        var center = (min.x + sideLength/2, min.y + sideLength/2, min.z + sideLength/2);
        var result = Octant.None;
        if (point.X >= center.Item1) result |= Octant.PositiveX;
        if (point.Y >= center.Item2) result |= Octant.PositiveY;
        if (point.Z >= center.Item3) result |= Octant.PositiveZ;
        return result;
    }

    public static void BuildNode(int nodeIndex, ref int currentTreeIndex, List<Node> nodes, ref VoxelMap voxelMap, (int x, int y, int z) min, int sideLength)
    {
        var childrenLength = sideLength / 2;
        var state = NodeState.None;
        byte materialId = 0;
        CheckUniformity(ref state, ref materialId, min.x, min.y, min.z, min.x + sideLength, min.y + sideLength, min.z + sideLength, voxelMap.IsSolid, voxelMap.TextureFor);
        
        if (state == NodeState.Empty || state == NodeState.Solid)
        {
            var material = state switch
            {
                NodeState.Solid => (byte)materialId,
                _ => (byte)0
            };
            nodes[nodeIndex] = new Node(0, state, material);
            return;
        }
        
        var childStartIndex = currentTreeIndex;
        currentTreeIndex += 8;
        
        var node = new Node((uint)childStartIndex, NodeState.Mixed);
        nodes[nodeIndex] = node;
        nodes.AddRange(Enumerable.Repeat(Empty, 8));

        for (int i = 0; i < 8; i++)
        {
            var octantMin = CalculateOctantBounds((Octant)i, min, childrenLength);
            BuildNode(childStartIndex + i, ref currentTreeIndex, nodes, ref voxelMap, octantMin, childrenLength);
        }
    }
    
    private static void CheckUniformity(ref NodeState state, ref byte materialId, int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
        Func<int, int, int, bool> isSolid, Func<int, int, int, byte> textureFor)
    {
        for (var x = minX; x < maxX; x++)
        for (var y = minY; y < maxY; y++)
        for (var z = minZ; z < maxZ; z++)
        {
            if (state == NodeState.Mixed) return;

            if (isSolid(x, y, z))
            {
                state |= NodeState.SolidVoxelFound;
                materialId = textureFor(x, y, z);
            }
            else
                state |= NodeState.EmptyVoxelFound;
        }
    }
    
    public static (int x, int y, int z) CalculateOctantBounds(Octant octant, (int x, int y, int z) min, int sideLength)
    {
        (int x, int y, int z) octantMinimum = (0, 0, 0);

        var hasPositiveX = (octant & Octant.PositiveX) != 0;
        var hasPositiveY = (octant & Octant.PositiveY) != 0;
        var hasPositiveZ = (octant & Octant.PositiveZ) != 0;

        octantMinimum.x = hasPositiveX ? min.x + sideLength : min.x;
        octantMinimum.y = hasPositiveY ? min.y + sideLength : min.y;
        octantMinimum.z = hasPositiveZ ? min.z + sideLength : min.z;

        return octantMinimum;
    }
}

public class Octree(Node[] nodes, uint oneSideLength)
{
    public Node[] Nodes { get; private set; } = nodes;

    public uint OneSideLength { get; private set; } = oneSideLength;
    
    public int MaxDepth => BitOperations.TrailingZeroCount(OneSideLength);

    public static Octree Create(ref VoxelMap voxelMap)
    {
        var nodes = new List<Node>();
        
        uint longestDimension = Math.Max(voxelMap.Dimensions.height,
            Math.Max(voxelMap.Dimensions.width, voxelMap.Dimensions.depth));
        if (!BitOperations.IsPow2(longestDimension))
            longestDimension = BitOperations.RoundUpToPowerOf2(longestDimension);
        
        var oneSideLength = longestDimension;

        nodes.Add(Node.Empty);
        var currentTreeIndex = 1;
        
        Node.BuildNode(0, ref currentTreeIndex, nodes, ref voxelMap, (0, 0, 0), (int)oneSideLength);
        
        if (nodes.Any(n => n.State == NodeState.None))
        {
            throw new ApplicationException("Some nodes are still None");
        }
        
        return new Octree([.. nodes], oneSideLength);
    }
}

public readonly struct TreeBranch(int x, int y, int z, uint nodeIndex, byte depth)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public uint NodeIndex { get; } = nodeIndex;
    public byte Depth { get; } = depth;

    public static TreeBranch Create<T>(int x, int y, int z, uint nodeIndex, T depth) where T : INumber<T> => new(x, y, z, nodeIndex, byte.CreateTruncating(depth));
}

public ref struct TreeClimber(Span<TreeBranch> branchBuffer, uint rootNodeSize)
{
    public Span<TreeBranch> BranchBuffer { get; } = branchBuffer;
    public byte CurrentDepth { get; private set; }
    
    private readonly uint _rootNodeSize = uint.CreateTruncating(rootNodeSize);

    public TreeBranch Climb(Vector3 point)
    {
        var currentBranch = BranchBuffer[CurrentDepth];
        while (CurrentDepth > 0 && !ContainsPoint(currentBranch, point))
        {
            currentBranch = BranchBuffer[--CurrentDepth];
        }
        return currentBranch;
    }
    
    public (Node, TreeBranch) Descend(Octree octree, Vector3 point)
    {
        CurrentDepth = 0;
        var currentBranch = BranchBuffer[0];
        var currentNode = octree.Nodes[currentBranch.NodeIndex];
        while (currentNode.State == NodeState.Mixed)
        {
            var octant = currentNode.GetOctant((currentBranch.X, currentBranch.Y, currentBranch.Z), _rootNodeSize >> CurrentDepth++, point);
            var (x, y, z) = Node.CalculateOctantBounds(octant, (currentBranch.X, currentBranch.Y, currentBranch.Z), (int)(_rootNodeSize >> CurrentDepth));
            
            var newNodeIndex = (int)(currentNode.ChildStartIndex + (int)octant);
            var newBranch = TreeBranch.Create(x, y, z, (uint)newNodeIndex, CurrentDepth);
            BranchBuffer[CurrentDepth] = newBranch;
            currentBranch = newBranch;
            currentNode = octree.Nodes[newNodeIndex];
        }
        return (currentNode, currentBranch);
    }

    private bool ContainsPoint(TreeBranch branch, Vector3 point)
    {
        var branchNodeSize = _rootNodeSize >> branch.Depth;
        var (maxX, maxY, maxZ) = (branch.X + (int)branchNodeSize, branch.Y + (int)branchNodeSize, branch.Z + (int)branchNodeSize);
        return point.X >= branch.X && point.X < maxX &&
               point.Y >= branch.Y && point.Y < maxY &&
               point.Z >= branch.Z && point.Z < maxZ;
    }
}