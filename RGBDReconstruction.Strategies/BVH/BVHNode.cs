using OpenTK.Mathematics;

namespace RGBDReconstruction.Strategies.BVH;

public struct BVHNode
{
    public Vector3 minPoint;
    public Vector3 maxPoint;
    public uint leftIdx;
    public uint rightIdx;
    public uint parentIdx;
    public uint objID;
}