using System.Runtime.InteropServices;
using System.Numerics;

namespace RGBDReconstruction.Strategies.BVH;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BVHNode {
    public Vector4 minPoint; // System.Numerics.Vector3 has the same layout as vec3 in GLSL
   // public float _padding1;  // Padding to match the GLSL alignment of vec3
    public Vector4 maxPoint;
    //public float _padding2; 
    public uint leftIdx;
    public uint rightIdx;
    public uint parentIdx;
    public uint objID;
}