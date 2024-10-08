﻿using Geometry;
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

    public ColouredMesh GetFrameGeometry(int frameNo)
    {
        // 0. tessellate each depth map to get voxel grid data
        var depthMapList = new List<float[,]>();
        var tessellatedDepthMapList = new List<Mesh>();
        var camPoseList = _viewProcessor.GetCameraPoseInformation(true);
        var camPoseListAlt = _viewProcessor.GetCameraPoseInformation(false);
        var numCams = camPoseList.Count;

        float xMin = float.PositiveInfinity;
        float xMax = float.NegativeInfinity;
        float yMin = float.PositiveInfinity;
        float yMax = float.NegativeInfinity;
        float zMin = float.PositiveInfinity;
        float zMax = float.NegativeInfinity;
        
        
        for (int i = 1; i <= numCams; i++)
        {

            
            depthMapList.Add(_viewProcessor.GetDepthMap(frameNo, i));
            var mesh = DepthTessellator.TessellateDepthArray(depthMapList[i-1], camPoseList[i-1]);

            
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
        for (int j = 0; j < numCams; j++)
        {
            // if (j > 1)
            // {
            //     continue;
            // }
            // TODO: Get camera pose from info
            var byteData = _viewProcessor.GetRGBImageData(frameNo, j + 1);
            var pose = camPoseListAlt[j];
            pose.Transpose();
            currentVoxGrid.UpdateWithTriangularMesh(tessellatedDepthMapList[j], pose, byteData, 1920, 1080);
        }

        // 3. do marching cubes and return
        
        var outputMesh = MarchingCubes.GenerateMeshFromVoxelGrid(currentVoxGrid);
        return outputMesh;
    }

    public List<Byte[]> GetRGBData(int frame)
    {
        return _viewProcessor.GetRGBImageDataAllCams(frame);
    }

    public ColouredMesh GetNextFrameGeometry()
    {
        _frame++;
        return GetFrameGeometry(_frame);
    }
}