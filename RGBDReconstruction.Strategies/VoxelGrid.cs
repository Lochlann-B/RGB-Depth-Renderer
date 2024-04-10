using System.Numerics;
using Geometry;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;
using Vector3 = OpenTK.Mathematics.Vector3;
using ILGPU;
using ILGPU.IR;
using ILGPU.Runtime;
// using SimpleScene.Util.ssBVH;

namespace RGBDReconstruction.Strategies;

public class VoxelGrid(int size, float xStart, float yStart, float zStart, float resolution) : IVoxelGrid
{
    protected float[] _voxelValues = new float[size * size * size];
    protected List<Vector3> _seenVoxels = new();
    protected float[] _voxelWeights = new float[size * size * size];

    public void UpdateWithTriangularMesh(Mesh triangularMeshInWorldCoords, Matrix4 cameraPose)
    {
        var mesh = triangularMeshInWorldCoords;
        var inter = 0;
        
        // 1. Get list of voxels near the mesh
        // Do this by taking each triangle, and adding enclosing voxel cube to list
        var closeVoxels = new HashSet<Vector3>();
        
        // var vertices = mesh.VertexPositions;
        // for (int i = 0; i < vertices.Count; i += 3)
        // {
        //    var vertexPos = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
        //    AddNeighbouringVoxels(closeVoxels, vertexPos);
        // }
        
        // TODO: Make parallel?
        GetVoxelsNearMesh(closeVoxels, mesh);
        Console.Write("All neighbouring voxels found! Length: {0}\n", closeVoxels.Count);

        // 2. Update voxel grid: 
        // Cast ray from viewer (cam pose) through voxels determined just now,
        // and then intersect with mesh.
        // Update voxel to be signed distance using formula described in the paper.

        // 1: For each voxel, construct a ray from cameraPose as source, to voxel as direction.
        var source = cameraPose.ExtractTranslation();
        
        

        var meshIntersectionTimes = new List<float>();
        Parallel.ForEach(closeVoxels, voxel =>
        //foreach(var voxel in closeVoxels)
        {
            var ray = new Ray(source, Vector3.Normalize(voxel - source));
            //Console.WriteLine("New ray constructed.");
            // 2: Get all triangles for which the ray intersects the mesh
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var triangleIntersections = ray.IntersectMesh(mesh);
            watch.Stop();
            meshIntersectionTimes.Add(watch.ElapsedMilliseconds);
            //Console.WriteLine("Ray intersection finished. Intersections: {0}", triangleIntersections.Count);
            // 3: For each triangle, get the signed distance from the voxel to the triangle along the camera viewing direction.
            var minDist = float.PositiveInfinity;
            inter += triangleIntersections.Count;
            foreach (var point in triangleIntersections)
            {
                var dist = Vector3.Distance(point, voxel);
                var distV = (voxel - ray.Source).Length;
                var distP = (point - ray.Source).Length;

                if (distV > distP)
                {
                    dist *= -1;
                }

                // 4: Update voxel's value to the smallest distance of these.
                minDist = float.MinMagnitude(dist, minDist);
            }

            if (minDist < float.PositiveInfinity)
            {
                this[voxel[0], voxel[1], voxel[2]] = minDist;
            }
            //Console.WriteLine("Voxel grid updated! \n");
        });
        
        Console.WriteLine("Number of triangle intersections: {0}\n", inter);
        
        Console.WriteLine("Times for ray intersection with mesh: \n Least: {0}\n Greatest: {1}\n Mean average: {2}", meshIntersectionTimes.Min(), meshIntersectionTimes.Max(), meshIntersectionTimes.Average());
    }

    protected void AddNeighbouringVoxels(HashSet<Vector3> voxels, Vector3 coord)
    {
        var startVox = new Vector3(
            coord[0].FloorToInterval(Resolution, XStart),
            coord[1].FloorToInterval(Resolution, YStart),
            coord[2].FloorToInterval(Resolution, ZStart)
        );

        voxels.Add(startVox);
        var v = startVox;
        var xInc = float.Min(xStart + Resolution * (Size - 2), v[0] + Resolution);
        var yInc = float.Min(yStart + Resolution * (Size - 2), v[1] + Resolution);
        var zInc = float.Min(zStart + Resolution * (Size - 2), v[2] + Resolution);
        voxels.Add(new Vector3(xInc, v[1], v[2]));
        voxels.Add(new Vector3(xInc, v[1], zInc));
        voxels.Add(new Vector3(v[0], v[1], zInc));
        voxels.Add(new Vector3(v[0], yInc, v[2]));
        voxels.Add(new Vector3(xInc, yInc, v[2]));
        voxels.Add(new Vector3(xInc, yInc, zInc));
        voxels.Add(new Vector3(v[0], yInc, zInc));
        
    }

    protected void GetVoxelsNearMesh(HashSet<Vector3> voxels, Mesh mesh)
    {
        var triangles = mesh.GetMeshTriangles();
        foreach (var triangle in triangles) 
        {
            // Get voxels near the triangle:
            // Loop through the bounding box of the triangle given by its smallest and largest x y z coords of
            // all 3 vertices.

            var smallestCoords = new[]
            {
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity
            };
            var largestCoords = new[]
            {
                float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity
            };
            foreach (var vertex in triangle.GetVerticesAsList())
            {
                if (vertex[2] < ZStart)
                {
                    var ass = 3;
                }
                for (int j = 0; j < 3; j++)
                {
                    smallestCoords[j] = float.Min(smallestCoords[j], vertex[j]);
                    largestCoords[j] = float.Max(largestCoords[j], vertex[j]);
                }
            }

            for (int j = 0; j < 3; j++)
            {
                var offset = j == 0 ? XStart : j == 1 ? YStart : ZStart;
                smallestCoords[j] = smallestCoords[j].FloorToInterval(Resolution, offset);
                largestCoords[j] = largestCoords[j].CeilToInterval(Resolution, offset);
            }

            for (var x = smallestCoords[0]; x <= largestCoords[0]; x += Resolution)
            {
                for (var y = smallestCoords[1]; y <= largestCoords[1]; y += Resolution)
                {
                    for (var z = smallestCoords[2]; z <= largestCoords[2]; z += Resolution)
                    {
                        AddNeighbouringVoxels(voxels, new Vector3(x, y, z));
                    }
                }
            }

            // TODO: Optimise this by looping through the coordinates on the surface of the triangle instead
        }
    }

    public float this[float x, float y, float z]
    {
        get => _voxelValues[Index(x, y, z)];
        set => _voxelValues[Index(x, y, z)] = value;
    }
    
    public float this[double x, double y, double z]
    {
        get => _voxelValues[Index(x, y, z)];
        set => _voxelValues[Index(x, y, z)] = value;
    }

    public List<Vector3> SeenVoxels => _seenVoxels;

    protected int Index(float x, float y, float z)
    {
        var nX = (x - XStart).RoundToInterval(Resolution);
        var nY = (y - YStart).RoundToInterval(Resolution);
        var nZ = (z - ZStart).RoundToInterval(Resolution);

        return (int)(nX / Resolution) + (int)((nY / Resolution) * Size) + (int)((nZ / Resolution) * Size * Size);
    }

    protected int Index(double x, double y, double z)
    {
        var nX = (x - xStart).RoundToInterval(Resolution);
        var nY = (y - YStart).RoundToInterval(Resolution);
        var nZ = (z - ZStart).RoundToInterval(Resolution);
        
        return (int)(nX / Resolution) + (int)((nY / Resolution) * Size) + (int)((nZ / Resolution) * Size * Size);
    }

    public float Resolution { get; } = resolution;
    public int Size { get; } = size;
    public float XStart { get; } = xStart;
    public float YStart { get; } = yStart;
    public float ZStart { get; } = zStart;
}