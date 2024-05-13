using System.Runtime.InteropServices;
using System.Numerics;

namespace RGBDReconstruction.Strategies.BVH;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BVHNode {
    public Vector4 minPoint; 

    public Vector4 maxPoint;

    public uint leftIdx;
    public uint rightIdx;
    public uint parentIdx;
    public uint objID;
}