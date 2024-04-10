using Geometry;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;

namespace RGBDReconstruction.Strategies;

public class MultiViewVoxelGridReconstruction
{
    protected MultiViewProcessor _viewProcessor;
    private int _frame = -1;
    protected int gridSize;
    
    public MultiViewVoxelGridReconstruction(int sceneNo, int voxGridSize)
    {
        gridSize = voxGridSize;
        switch (sceneNo)
        {
            case 0:
                _viewProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\chain_collision");
                break;
        }
    }

    public Mesh GetFrameGeometry(int frameNo)
    {
        // 0. tessellate each depth map to get voxel grid data
        var depthMapList = new List<float[,]>();
        var tessellatedDepthMapList = new List<Mesh>();
        var camPoseList = _viewProcessor.GetCameraPoseInformation();
        var numCams = camPoseList.Count;

        float xMin = float.PositiveInfinity;
        float xMax = float.NegativeInfinity;
        float yMin = float.PositiveInfinity;
        float yMax = float.NegativeInfinity;
        float zMin = float.PositiveInfinity;
        float zMax = float.NegativeInfinity;
        
        
        for (int i = 6; i <= numCams; i++)
        {
            // if (i > 2)
            // {
            //     continue;
            // }
            
            depthMapList.Add(_viewProcessor.GetDepthMap(frameNo, i-5));
            var mesh = DepthTessellator.TessellateDepthArray(depthMapList[i-6], camPoseList[i-6]);
            tessellatedDepthMapList.Add(mesh);
            
            xMin = Math.Min(mesh.xRanges[0], xMin);
            xMax = Math.Max(mesh.xRanges[1], xMax);
            yMin = Math.Min(mesh.yRanges[0], yMin);
            yMax = Math.Max(mesh.yRanges[1], yMax);
            zMin = Math.Min(mesh.zRanges[0], zMin);
            zMax = Math.Max(mesh.zRanges[1], zMax);
        }
        
        var resX = (xMax - xMin) / (gridSize-2);
        var resY = (yMax - yMin) / (gridSize-2);
        var resZ = (zMax - zMin) / (gridSize-2);
        var res = Math.Max(resX, Math.Max(resY, resZ));
        
        // 1. initialise voxel grid
        var currentVoxGrid = new VoxelGridDeviceBVH(gridSize, xMin, yMin, zMin, res);

        // 2. for each camera:
        // b. get camera transformation data
        // c. update voxel grid
        for (int j = 5; j < numCams; j++)
        {
            // if (j > 1)
            // {
            //     continue;
            // }
            // TODO: Get camera pose from info
            currentVoxGrid.UpdateWithTriangularMesh(tessellatedDepthMapList[j-5], camPoseList[j-5]);
        }

        // 3. do marching cubes and return
        var outputMesh = MarchingCubes.GenerateMeshFromVoxelGrid(currentVoxGrid);
        return outputMesh;
    }

    public Mesh GetNextFrameGeometry()
    {
        _frame++;
        return GetFrameGeometry(_frame);
    }
}