using System.Diagnostics;
using Geometry;
using ILGPU.Runtime;
using OpenTK.Mathematics;
using OpenTKDowngradeHelper;
using RGBDReconstruction.Application;
using RGBDReconstruction.Strategies.BVH;
using SimpleScene;
using SimpleScene.Util.ssBVH;

namespace RGBDReconstruction.Strategies;

public class VoxelGridBVH(int size, float xStart, float yStart, float zStart, float resolution) : VoxelGrid(size, xStart, yStart, zStart, resolution)
{
    public new void UpdateWithTriangularMesh(Mesh triangularMeshInWorldCoords, Matrix4 cameraPose)
    {
        var mesh = triangularMeshInWorldCoords;
        var inter = 0;
        var watch = new System.Diagnostics.Stopwatch();
        
        // 1a. Get list of voxels near the mesh
        // Do this by taking each triangle, and adding enclosing voxel cube to list
        var closeVoxels = new HashSet<Vector3>();
        
        // TODO: Make parallel?
        Console.WriteLine("Finding voxels near mesh start...");
        watch.Start();
        //GetVoxelsNearMesh(closeVoxels, mesh);
        watch.Stop();
        Console.Write("All neighbouring voxels found! Length: {0}. Time: {1}ms \n", closeVoxels.Count, watch.ElapsedMilliseconds);
        watch.Reset();
        
        // 1b. Create BVH using mesh's triangle list
        Console.WriteLine("Setting up BVH start...");
        watch.Start();
        var triangleList = mesh.GetMeshTriangles();
        
        //var triangleBVH = new ssBVH<Triangle>(new TriangleBVHNodeAdaptor(), triangleList);
        var triangleBVH = BVHConstructor.GetBVH(mesh.VertexPositions.ToArray(), mesh.MeshLayout.IndexArray, mesh.xRanges.ToArray(), mesh.yRanges.ToArray(), mesh.zRanges.ToArray());
        watch.Stop();
        Console.WriteLine("BVH setup finished. Time: {0}ms \n", watch.ElapsedMilliseconds);
        watch.Reset();
        
        // 2. Update voxel grid: 
        // Cast ray from viewer (cam pose) through voxels determined just now,
        // and then intersect with mesh.
        // Update voxel to be signed distance using formula described in the paper.

        // 1: For each voxel, construct a ray from cameraPose as source, to voxel as direction.
        var source = cameraPose.ExtractTranslation();
        
        

        // var meshIntersectionTimes = new List<float>();
        // var watch2 = new Stopwatch();
        // Console.WriteLine("Intersecting rays from camera through voxel into mesh start...");
        // watch2.Start();
        // //Parallel.ForEach(closeVoxels, voxel =>
        // foreach(var voxel in closeVoxels)
        // {
        //     var ray = new Ray(source, Vector3.Normalize(voxel - source));
        //     //Console.WriteLine("New ray constructed.");
        //     // 2: Get all triangles for which the ray intersects the mesh
        //     watch.Start();
        //
        //
        //     var triangleIntersectionSSList = SimpleSceneCommunicator.GetRayHits(triangleBVH, ray);
        //     var triangleIntersections =
        //         triangleIntersectionSSList.Select(node => node.gobjects).Where(obj => obj != null).SelectMany(l => l)
        //             .ToList();
        //
        //     watch.Stop();
        //     meshIntersectionTimes.Add(watch.ElapsedMilliseconds);
        //     watch.Reset();
        //     //Console.WriteLine("Ray intersection finished. Intersections: {0}", triangleIntersections.Count);
        //     // 3: For each triangle, get the signed distance from the voxel to the triangle along the camera viewing direction.
        //     var minDist = float.PositiveInfinity;
        //     foreach (var triangle in triangleIntersections)
        //     {
        //         var nullablePoint = ray.GetIntersectionPoint(triangle);
        //         if (nullablePoint == null)
        //         {
        //             continue;
        //         }
        //
        //         inter++;
        //
        //         var point = nullablePoint.Value;
        //         var dist = Vector3.Distance(point, voxel);
        //         var distV = (voxel - ray.Source).Length;
        //         var distP = (point - ray.Source).Length;
        //
        //         if (distV > distP)
        //         {
        //             dist *= -1;
        //         }
        //
        //         // 4: Update voxel's value to the smallest distance of these.
        //         minDist = float.MinMagnitude(dist, minDist);
        //     }
        //
        //     //if (minDist < float.PositiveInfinity)
        //     //{
        //         this[voxel[0], voxel[1], voxel[2]] = minDist;
        //     //}
        //     //Console.WriteLine("Voxel grid updated! \n");
        //     if (minDist < float.PositiveInfinity)
        //     {
        //         _seenVoxels.Add(new Vector3(voxel[0], voxel[1], voxel[2]));
        //     }
        // }
        // watch2.Stop();
        //
        // Console.WriteLine("Number of triangle intersections: {0}\n", inter);
        //
        // Console.WriteLine("Times for ray intersection with mesh: \n Least: {0}ms\n Greatest: {1}ms\n Mean average: {2}ms", meshIntersectionTimes.Min(), meshIntersectionTimes.Max(), meshIntersectionTimes.Average());
        // Console.WriteLine("Total time for all ray intersections: {0}ms", watch2.ElapsedMilliseconds);
    }
}