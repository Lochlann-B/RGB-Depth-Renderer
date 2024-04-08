using System.Diagnostics;
using System.Runtime.InteropServices;
using Geometry;
using OpenTK.Graphics.OpenGL;
using System.Numerics;
using OpenTK.Mathematics;
// using OpenTKDowngradeHelper;
using RGBDReconstruction.Strategies.BVH;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace RGBDReconstruction.Strategies;

public class VoxelGridDeviceBVH(int size, float xStart, float yStart, float zStart, float resolution) : VoxelGrid(size, xStart, yStart, zStart, resolution)
{
    public BVHNode[] BVH { get; set; }
    private ComputeShader _computeShader = new("./ComputeShaders/VoxelGridRayTracer.glsl");
    
    public new void UpdateWithTriangularMesh(Mesh triangularMeshInWorldCoords, Matrix4 cameraPose)
    {
        var indexArray = triangularMeshInWorldCoords.MeshLayout.IndexArray.ToArray();
        var posArray = triangularMeshInWorldCoords.VertexPositions.ToArray();

        var xRanges = triangularMeshInWorldCoords.xRanges.ToArray();
        var yRanges = triangularMeshInWorldCoords.yRanges.ToArray();
        var zRanges = triangularMeshInWorldCoords.zRanges.ToArray();

        var cameraPos = cameraPose.ExtractTranslation();

        var seenVoxels = new System.Numerics.Vector4[Size * Size * Size];
        
        var closeVoxels = new HashSet<System.Numerics.Vector4>();
        
        // TODO: Make parallel?
        var watch = new Stopwatch();
        Console.WriteLine("Finding voxels near mesh start...");
        watch.Start();
        GetVoxelsNearMesh(closeVoxels, triangularMeshInWorldCoords);
        watch.Stop();
        Console.Write("All neighbouring voxels found! Length: {0}. Time: {1}ms \n", closeVoxels.Count, watch.ElapsedMilliseconds);
        watch.Reset();

        var closeVoxelData = closeVoxels.ToArray();
        
        BVH = BVHConstructor.GetBVH(posArray, indexArray, xRanges, yRanges, zRanges);
        
        // blep(closeVoxels.ToArray(), cameraPos, BVH, (BVH.Length + 1)/2, posArray, indexArray);
        // return;
        
       // int numLeaves = (BVH.Length + 1) / 2;
        //int reachableLeaves = HowManyLeafNodes(BVH, numLeaves);
        
        _computeShader.Use();
        
        _computeShader.SetUniformInt("numObjs", (BVH.Length+1)/2);
        _computeShader.SetUniformFloat("resolution", Resolution);
        _computeShader.SetUniformInt("size", Size);
        _computeShader.SetUniformFloat("xStart", XStart);
        _computeShader.SetUniformFloat("yStart", YStart);
        _computeShader.SetUniformFloat("zStart", ZStart);
        _computeShader.SetUniformVec3("cameraPos", ref cameraPos);
        
        int positionBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, positionBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float)*posArray.Length, posArray, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, positionBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int indexBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, indexBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int)*indexArray.Length, indexArray, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, indexBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int BVHNodesBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHNodesBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>()*BVH.Length, BVH, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, BVHNodesBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int voxelValuesBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, voxelValuesBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float)*_voxelValues.Length, _voxelValues, BufferUsageHint.StaticDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, voxelValuesBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int seenVoxelsBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, seenVoxelsBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<System.Numerics.Vector4>()*seenVoxels.Length, seenVoxels, BufferUsageHint.StaticDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, seenVoxelsBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int closeVoxelsBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, closeVoxelsBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<System.Numerics.Vector4>()*closeVoxelData.Length, closeVoxelData, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, closeVoxelsBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        int atomicCounterBufferID = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.AtomicCounterBuffer, atomicCounterBufferID);
        GL.BufferData(BufferTarget.AtomicCounterBuffer, (IntPtr)(sizeof(uint)), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 6, atomicCounterBufferID);
        GL.BindBuffer(BufferTarget.AtomicCounterBuffer, 0);
        
            
       //  GL.DispatchCompute(closeVoxelData.Length, 1, 1);
       //  //GL.MemoryBarrier(MemoryBarrierFlags.AtomicCounterBarrierBit);
       // // GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
       //  GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
        
        _computeShader.SetUniformInt("groupSize", 10000);
        for (int groupIdx = 0; groupIdx * 10000 < closeVoxelData.Length; groupIdx++)
        {
            _computeShader.SetUniformInt("groupIdx", groupIdx);
         
            GL.DispatchCompute(Math.Min(10000, closeVoxelData.Length-10000*groupIdx), 1, 1);
            // GL.DispatchCompute(closeVoxelData.Length, 1, 1);
            //GL.MemoryBarrier(MemoryBarrierFlags.AtomicCounterBarrierBit);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }
        
        Console.WriteLine(GL.GetError());
        
        uint counterValue = 0;
        GL.GetNamedBufferSubData(seenVoxelsBufferSSBO, 0, Marshal.SizeOf<System.Numerics.Vector4>() * seenVoxels.Length, ref seenVoxels[0]);
        GL.GetNamedBufferSubData(voxelValuesBufferSSBO, 0, sizeof(float) * _voxelValues.Length, _voxelValues);
       // GL.GetNamedBufferSubData(atomicCounterBufferID, 0, sizeof(int), ref counterValue);
        //GL.CopyBufferSubData();
        //GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        //GL.Flush();
        var seenVoxHashSet = new HashSet<Vector3>();
        for (int i = 0; i < seenVoxels.Length; i++)
        {
            seenVoxHashSet.Add(new Vector3(seenVoxels[i][0], seenVoxels[i][1], seenVoxels[i][2]));
            //_seenVoxels.Add(new Vector3(seenVoxels[i][0], seenVoxels[i][1], seenVoxels[i][2]));
        }

        foreach (var vox in seenVoxHashSet)
        {
            _seenVoxels.Add(vox);
        }
    }

    private int HowManyLeafNodes(BVHNode[] tree, int numObjs)
    {
        int count = 0;
        int[] stack = new int[2*numObjs];
        var visitedNodes = new HashSet<int>();

        var leafIdxs = new HashSet<uint>();

        int stackPointer = 1;

        int numIters = 0;

        while (stackPointer > 0)
        {
            numIters++;
            if (numIters > 10000000)
            {
                return -1;
            }
            
            stackPointer--;

            if (!visitedNodes.Add(stack[stackPointer]))
            {
                continue;
            }

            BVHNode currNode = tree[stack[stackPointer]];
            
            uint leftIdx = currNode.leftIdx;
            uint rightIdx = currNode.rightIdx;

            if (leftIdx > numObjs - 1)
            {
                leafIdxs.Add(leftIdx);
            }
            else
            {
                stack[stackPointer] = (int)currNode.leftIdx;
                stackPointer++;
            }

            if (rightIdx > numObjs - 1)
            {
                leafIdxs.Add(rightIdx);
            }
            else
            {
                stack[stackPointer] = (int)currNode.rightIdx;
                stackPointer++;
            }
        }

        return leafIdxs.Count;
    }
    
    protected void GetVoxelsNearMesh(HashSet<System.Numerics.Vector4> voxels, Mesh mesh)
    {

        // for (float i = 0; i < Size; i++)
        // {
        //     for (float j = 0; j < Size; j++)
        //     {
        //         for (float k = 0; k < Size; k++)
        //         {
        //             voxels.Add(new System.Numerics.Vector4(XStart + i * Resolution, YStart + j * Resolution,
        //                 ZStart + k * Resolution, 1.0f));
        //         }
        //     }
        // }
        //
        // return;  
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
                for (int j = 0; j < 3; j++)
                {
                    smallestCoords[j] = float.Min(smallestCoords[j], vertex[j]);
                    largestCoords[j] = float.Max(largestCoords[j], vertex[j]);
                }
            }

            for (int j = 0; j < 3; j++)
            {
                smallestCoords[j] = smallestCoords[j].FloorToInterval(Resolution);
                largestCoords[j] = largestCoords[j].CeilToInterval(Resolution);
            }

            var resXm = smallestCoords[0] - 0*Resolution <= mesh.xRanges[0]
                ? smallestCoords[0]
                : smallestCoords[0] - 0*Resolution;
            var resXl = largestCoords[0] + 0*Resolution >= mesh.xRanges[1]
                ? largestCoords[0]
                : largestCoords[0] + 0*Resolution;
            var resYm = smallestCoords[1] - 0*Resolution <= mesh.yRanges[0]
                ? smallestCoords[1]
                : smallestCoords[1] - 0*Resolution;
            var resYl = largestCoords[1] + 0*Resolution >= mesh.yRanges[1]
                ? largestCoords[1]
                : largestCoords[1] + 0*Resolution;
            var resZm = smallestCoords[2] - 0*Resolution <= mesh.zRanges[0]
                ? smallestCoords[2]
                : smallestCoords[2] - 0*Resolution;
            var resZl = largestCoords[2] + 0*Resolution >= mesh.zRanges[1]
                ? largestCoords[2]
                : largestCoords[2] + 0*Resolution;

            for (var x = resXm; x <= resXl; x += Resolution)
            {
                for (var y = resYm; y <= resYl; y += Resolution)
                {
                    for (var z = resZm; z <= resZl; z += Resolution)
                    {
                        AddNeighbouringVoxels(voxels, new Vector3(x, y, z));
                    }
                }
            }

            // TODO: Optimise this by looping through the coordinates on the surface of the triangle instead
        }
    }
    
    protected void AddNeighbouringVoxels(HashSet<System.Numerics.Vector4> voxels, Vector3 coord)
    {
        // var startVox = new System.Numerics.Vector4(
        //     coord[0].FloorToInterval(Resolution),
        //     coord[1].FloorToInterval(Resolution),
        //     coord[2].FloorToInterval(Resolution),
        //     1.0f
        // );
        var startVox = new System.Numerics.Vector4(coord[0], coord[1], coord[2], 1.0f);

        voxels.Add(startVox);
        var v = startVox;
        var xInc = float.Min(xStart + Resolution * (Size - 2), v[0] + Resolution);
        var yInc = float.Min(yStart + Resolution * (Size - 2), v[1] + Resolution);
        var zInc = float.Min(zStart + Resolution * (Size - 2), v[2] + Resolution);
        voxels.Add(new System.Numerics.Vector4(xInc, v[1], v[2],1f));
        voxels.Add(new System.Numerics.Vector4(xInc, v[1], zInc,1f));
        voxels.Add(new System.Numerics.Vector4(v[0], v[1], zInc,1f));
        voxels.Add(new System.Numerics.Vector4(v[0], yInc, v[2],1f));
        voxels.Add(new System.Numerics.Vector4(xInc, yInc, v[2],1f));
        voxels.Add(new System.Numerics.Vector4(xInc, yInc, zInc,1f));
        voxels.Add(new System.Numerics.Vector4(v[0], yInc, zInc,1f));
        
    }

int indexx(float px, float py, float pz) {
    float nX = (px - XStart).RoundToInterval(Resolution);
    float nY = (py - YStart).RoundToInterval(Resolution);
    float nZ = (pz - ZStart).RoundToInterval(Resolution);

    return (int)(nX / resolution) + (int)((nY / resolution) * size) + (int)((nZ / resolution) * size * size);
}

    bool intersectsPlane(float minCoord, float maxCoord, Vector3 n, Vector3 raySource, Vector3 rayDirection, Vector3 axis1, Vector2 bounds1, Vector3 axis2, Vector2 bounds2) {
        float denominator = Vector3.Dot(n, rayDirection);
        bool doesIntersect = false;
        if (denominator != 0) {
            // min
            float d = minCoord;
            float t = (d - Vector3.Dot(n, raySource))/(denominator);
            if (t >= 0) {
                Vector3 posmin = raySource + t*rayDirection;
                doesIntersect = doesIntersect || ((bounds1[0] <= Vector3.Dot(axis1, posmin)) && (bounds1[1] >= Vector3.Dot(axis1, posmin)) && (bounds2[0] <= Vector3.Dot(axis2, posmin)) && (bounds2[1] >= Vector3.Dot(axis2, posmin)));
            }

            // max
            d = maxCoord;
            t = (d - Vector3.Dot(n, raySource))/(denominator);
            if (t >= 0) {
                Vector3 posmax = raySource + t*rayDirection;
                doesIntersect = doesIntersect || ((bounds1[0] <= Vector3.Dot(axis1, posmax)) && (bounds1[1] >= Vector3.Dot(axis1, posmax)) && (bounds2[0] <= Vector3.Dot(axis2, posmax)) && (bounds2[1] >= Vector3.Dot(axis2, posmax)));
            }
        }
        
        return doesIntersect;
    }

    bool rayIntersectsAABB(Vector3 raySource, Vector3 rayDirection, BVHNode node) {
        // We need to check the intersection with 6 planes
        // Plane is defined as n.r = d
        // Ray is defined as r = s + td
        
        bool doesIntersect = false;
        
        float minX = node.minPoint[0];
        float maxX = node.maxPoint[0];
        float minY = node.minPoint[1];
        float maxY = node.maxPoint[1];
        float minZ = node.minPoint[2];
        float maxZ = node.maxPoint[2];
        
        // x-axis planes
        doesIntersect = doesIntersect || intersectsPlane(minX, maxX, new Vector3(1, 0, 0), raySource, rayDirection, new Vector3(0, 1, 0), new Vector2(minY, maxY), new Vector3(0, 0, 1), new Vector2(minZ, maxZ));
        
        // y-axis planes
        doesIntersect = doesIntersect || intersectsPlane(minY, maxY, new Vector3(0, 1, 0), raySource, rayDirection, new Vector3(1, 0, 0), new Vector2(minX, maxX), new Vector3(0, 0, 1), new Vector2(minZ, maxZ));
        
        // z-axis planes
        doesIntersect = doesIntersect || intersectsPlane(minZ, maxZ, new Vector3(0, 0, 1), raySource, rayDirection, new Vector3(0, 1, 0), new Vector2(minY, maxY), new Vector3(1, 0, 0), new Vector2(minX, maxX));
        
        return doesIntersect;
    }

    Vector3 getNormal(Vector3 v1, Vector3 v2, Vector3 v3) {
        return Vector3.Normalize(Vector3.Cross(v2 - v1, v3 - v2));
    }

    bool pointInsideTriangle(Vector3 p, Vector3 n, Vector3 v1, Vector3 v2, Vector3 v3) {
        Vector3 c1 = p - v1;
        Vector3 c2 = p - v2;
        Vector3 c3 = p - v3;
        
        return Vector3.Dot(n, Vector3.Cross(v2 - v1, c1)) > 0 && Vector3.Dot(n, Vector3.Cross(v3 - v2, c2)) > 0 && Vector3.Dot(n, Vector3.Cross(v1 - v3, c3)) > 0;
    }

    Vector4 rayIntersectsTriangle(Vector3 raySource, Vector3 rayDirection, BVHNode leafNode, float[] positionArray, int[] indexArray) {
        uint objId = leafNode.objID;

        int i1 = (int)(3*objId);
        int i2 = i1 + 1;
        int i3 = i1 + 2;

        Vector3 v1 = new Vector3(positionArray[3*indexArray[i1]],
        positionArray[3*indexArray[i1]+1],
        positionArray[3*indexArray[i1]+2]);

        Vector3 v2 = new Vector3(positionArray[3*indexArray[i2]],
        positionArray[3*indexArray[i2]+1],
        positionArray[3*indexArray[i2]+2]);

        Vector3 v3 = new Vector3(positionArray[3*indexArray[i3]],
        positionArray[3*indexArray[i3]+1],
        positionArray[3*indexArray[i3]+2]);
        
        Vector3 n = getNormal(v1, v2, v3);
        
        if (Math.Abs(Vector3.Dot(n, rayDirection)) < 1e-6) {
            return new Vector4(0, 0, 0, 0);
        }
        
        float d = Vector3.Dot(n, v1);
        
        float t = (d - Vector3.Dot(n, raySource)) / Vector3.Dot(n, rayDirection);
        
        if (t < 0) {
            return new Vector4(0,0,0,0);
        }
        
        Vector3 p = raySource + t * rayDirection;
        
        if (pointInsideTriangle(p, n, v1, v2, v3)) {
            return new Vector4(p[0], p[1], p[2], 1.0f);
        }
        
        return new Vector4(0,0,0,0);
    }

    float minMagnitude(float f1, float f2) {
        float t1 = f1 < 0 ? f1 * -1 : f1;
        float t2 = f2 < 0 ? f2 * -1 : f2;
        
        if (t1 < t2) {
            return f1;
        }
        
        return f2;
    }

    private void blep(System.Numerics.Vector4[] closeVoxels, Vector3 cameraPos, BVHNode[] nodes, int numObjs, float[] positionArray, int[] indexArray)
    {
        var seenVoxList = new HashSet<Vector3>();
        for (int idx = 0; idx < closeVoxels.Length; idx++)
        {

            Vector3 voxel = new Vector3(closeVoxels[idx][0], closeVoxels[idx][1], closeVoxels[idx][2]);

            Vector3 raySource = cameraPos;
            Vector3 rayDirection = Vector3.Normalize(voxel - raySource);

            //int rayIntersectObjIds[50];

            // Do ray intersection
            int[] stack = new int[200];
            int stackPointer = 0;

            stack[stackPointer] = 0;
            stackPointer++;

            float minDist = 1 / 0f;
            int iters = -1;

            while (stackPointer > 0)
            //for (int ble = 0; ble < 5*(numObjs - 1); ble++)
            {

                if (stackPointer <= 0)
                {
                    break;
                }
                iters++;
                if (iters > 5000000)
                {
                    var doo = 2;
                }

                stackPointer--;
                int index = stack[stackPointer];
                BVHNode node = nodes[index];

                bool isLeaf = index >= numObjs - 1;

                if (isLeaf)
                {
                    Vector4 point = rayIntersectsTriangle(raySource, rayDirection, node, positionArray, indexArray);
                    if (point[3] != 0)
                    {
                        Vector3 nPoint = new Vector3(point[0], point[1], point[2]);
                        float dist = OpenTK.Mathematics.Vector3.Distance(nPoint, voxel);
                        float distV = (voxel - raySource).Length;
                        float distP = (nPoint - raySource).Length;

                        if (distV > distP)
                        {
                            dist *= -1;
                        }

                        minDist = minMagnitude(dist, minDist);
                        var idxx = indexx(voxel[0], voxel[1], voxel[2]);
                        
                    }
                }
                else
                {
                    if (node.leftIdx != 0 && rayIntersectsAABB(raySource, rayDirection, nodes[node.leftIdx]))
                    {
                        stack[stackPointer] = (int)(node.leftIdx);
                        stackPointer++;
                    }

                    if (node.rightIdx != 0 && rayIntersectsAABB(raySource, rayDirection, nodes[node.rightIdx]))
                    {
                        stack[stackPointer] = (int)(node.rightIdx);
                        stackPointer++;
                    }
                }
            }

            seenVoxList.Add(voxel);
            if (minDist < float.PositiveInfinity)
            {
                this[voxel[0], voxel[1], voxel[2]] = minDist;
            }

        }

        _seenVoxels = [..seenVoxList];
    }
    
}