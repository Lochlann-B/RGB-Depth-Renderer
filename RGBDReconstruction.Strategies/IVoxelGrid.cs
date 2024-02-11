using Geometry;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;

namespace RGBDReconstruction.Strategies;

public interface IVoxelGrid
{
    public float this[float x, float y, float z] { get; set; }
    
    public List<Vector3> SeenVoxels { get; }
    
    public float Resolution { get; }
    public int Size { get; }
    public float XStart { get; }
    public float YStart { get; }
    public float ZStart { get; }

    public void UpdateWithTriangularMesh(Mesh triangleMeshInWorldCoords, Matrix4 cameraPose);
}