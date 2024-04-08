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
                _viewProcessor = new MultiViewProcessor("BLEP CHANGE ME");
                break;
        }
    }

    private String GetDepthMapFileName(int frame, int cam)
    {
        var numFrameDigits = (int) Math.Ceiling(Math.Log10(frame));
        var numCamDigits = (int)Math.Ceiling(Math.Log10(cam));
        
        // max is 4 digits
        var frameStr = "";
        var camStr = "";
        for (int f = 0; f < (4 - numFrameDigits); f++)
        {
            frameStr += "0";
        }

        frameStr += numFrameDigits.ToString();
        
        for (int c = 0; c < (4 - numCamDigits); c++)
        {
            camStr += "0";
        }

        camStr += numCamDigits.ToString();

        return "frame_" + frameStr + "_cam_" + camStr + ".exr";
    }

    public Mesh GetFrameGeometry(int frameNo)
    {
        // 0. tessellate each depth map to get voxel grid data
        int numCams = 6; // TODO: obtain from scene file with camera transformation data
        var depthMapList = new List<float[,]>();
        var tessellatedDepthMapList = new List<Mesh>();

        float xMin = float.PositiveInfinity;
        float xMax = float.NegativeInfinity;
        float yMin = float.PositiveInfinity;
        float yMax = float.NegativeInfinity;
        float zMin = float.PositiveInfinity;
        float zMax = float.NegativeInfinity;
        
        
        for (int i = 1; i <= numCams; i++)
        {
            depthMapList.Add(RGBDepthPoseInputProcessor.GetCameraLocalDepthMapFromExrFile(GetDepthMapFileName(frameNo, i)));
            var mesh = DepthTessellator.TessellateDepthArray(depthMapList[i-1]);
            tessellatedDepthMapList.Add(mesh);
            
            xMin = Math.Min(mesh.xRanges[0], xMin);
            xMax = Math.Min(mesh.xRanges[1], xMax);
            yMin = Math.Min(mesh.yRanges[0], yMin);
            yMax = Math.Min(mesh.yRanges[1], yMax);
            zMin = Math.Min(mesh.zRanges[0], zMin);
            zMax = Math.Min(mesh.zRanges[1], zMax);
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
            // TODO: Get camera pose from info
            currentVoxGrid.UpdateWithTriangularMesh(tessellatedDepthMapList[j], Matrix4.Identity);
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