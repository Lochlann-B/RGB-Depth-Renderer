using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace RGBDReconstruction.Strategies.BVH;

public class BVHConstructor
{
    private static BVHNode[] GenerateHierarchy(uint[] sortedMortonCodes, int[] sortedObjIDs, int numObjects)
    {
        var leafNodes = new BVHNode[numObjects];
        var internalNodes = new BVHNode[numObjects - 1];
        
        // Construct leaf nodes
        
        // TODO: Call compute shader which attributes object IDs to leaf nodes
        Parallel.For(0, numObjects, i =>
        {
            var leafNode = new BVHNode();
            leafNode.objID = (uint) sortedObjIDs[i];
            leafNodes[i] = leafNode;
        });
        
        // Construct internal nodes
        
        // TODO: Call compute shader which does the following for each internal node:
        // 1. Determine the range (see paper)
        // 2. Determine the split of the range
        // 3. Select the first child from either the leaf node list or internal node list
        // 4. Select the second child from either the leaf node list or the internal node list
        // 5. Set each child's parent to the current internal node

        BVHGenerationComputeShader(internalNodes, leafNodes, numObjects, sortedMortonCodes);

        // TODO: Return the list of nodes given by each Compute Shader
        var BVHNodes = new BVHNode[2 * numObjects - 1];
        internalNodes.CopyTo(BVHNodes, 0);
        leafNodes.CopyTo(BVHNodes, numObjects-1);

        return BVHNodes;
    }

    public static BVHNode[] GetBVH(float[] positionArray, int[] indexArray)
    {
        var unsortedTriangleIdxArray = Enumerable.Range(0, indexArray.Length / 3).ToArray();
        
        var (sortedMortonCodes, sortedObjIdxs) = SortMortonCodesComputeShader(positionArray, indexArray, unsortedTriangleIdxArray);

        return GenerateHierarchy(sortedMortonCodes, sortedObjIdxs, sortedObjIdxs.Length);
    }

    private static (uint[], int[]) SortMortonCodesComputeShader(float[] positionArray, int[] indexArray, int[] unsortedIdxArray)
    {
        var mortonCodes = GetMortonCodesComputeShader(indexArray, positionArray);
        var watch = Stopwatch.StartNew();
        var mortonCodesCopy = new uint[mortonCodes.Length];
        Array.Copy(mortonCodes, mortonCodesCopy, mortonCodes.Length);
        
        Array.Sort(mortonCodesCopy);

        var sortedIdxArray = new int[unsortedIdxArray.Length];

        for (int i = 0; i < mortonCodesCopy.Length; i++)
        {
            int originalIdx = Array.IndexOf(mortonCodes, mortonCodesCopy[i]);
            sortedIdxArray[i] = unsortedIdxArray[originalIdx];
        }
        watch.Stop();
        Console.WriteLine("Time taken to sort morton codes: {0}ms", watch.ElapsedMilliseconds);
        watch.Reset();

        return (mortonCodesCopy, sortedIdxArray);
    }

    private static uint[] GetMortonCodesComputeShader(int[] indexBuffer, float[] positionBuffer)
    {
        var mortonCodeData = new uint[indexBuffer.Length/3];
        
        var computeShader = new ComputeShader("./ComputeShaders/MortonCodeComputeShader.glsl");
        
        computeShader.Use();
        
        computeShader.SetUniformIntArray("indexBuffer", ref indexBuffer);
        computeShader.SetUniformFloatArray("positionBuffer", ref positionBuffer);
        
        // Generate buffer handles
        var mortonCodeBufferHandle = GL.GenBuffer();
        
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mortonCodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(uint)*mortonCodeData.Length, mortonCodeData, BufferUsageHint.StaticDraw);
        
        // Bind the SSBOs to read their data
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, mortonCodeBufferHandle);
        
        watch.Stop();
        Console.WriteLine("Morton code compute shader compile time: {0}ms", watch.ElapsedMilliseconds);

        watch.Reset();
        watch.Start();
        GL.DispatchCompute(mortonCodeData.Length, 1, 1);
        watch.Stop();
        Console.WriteLine("Morton code generation time: {0}ms", watch.ElapsedMilliseconds);
        
        
        // read the data
        GL.GetNamedBufferSubData(mortonCodeBufferHandle, 0, sizeof(uint) * mortonCodeData.Length, mortonCodeData);

        return mortonCodeData;
    }

    private static void BVHGenerationComputeShader(BVHNode[] internalNodes, BVHNode[] leafNodes, int numObjs, uint[] sortedMortonCodes)
    {
         var computeShader = new ComputeShader("./ComputeShaders/BVHComputeShader.glsl");
        
        computeShader.Use();
        
        computeShader.SetUniformInt("numObjs", numObjs);
        computeShader.SetUniformUIntArray("sortedMortonCodes", ref sortedMortonCodes);
        
        // Generate buffer handles
        int BVHInternalNodeBufferHandle = GL.GenBuffer();
        int BVHLeafNodeBufferHandle = GL.GenBuffer();
        
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHInternalNodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>()*internalNodes.Length, internalNodes, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHLeafNodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>() * leafNodes.Length, leafNodes, BufferUsageHint.StaticDraw);
        
        // Bind the SSBOs to read their data
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, BVHInternalNodeBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, BVHLeafNodeBufferHandle);
        
        
        
        watch.Stop();
        Console.WriteLine("BVH Construction Compute shader compile time: {0}ms", watch.ElapsedMilliseconds);

        watch.Reset();
        watch.Start();
        GL.DispatchCompute(numObjs - 1, 1, 1);
        watch.Stop();
        Console.WriteLine("BVH Construction time (GPU): {0}ms", watch.ElapsedMilliseconds);
        
        // read the data
        GL.GetNamedBufferSubData(BVHInternalNodeBufferHandle, 0, Marshal.SizeOf<BVHNode>() * internalNodes.Length, internalNodes);
        GL.GetNamedBufferSubData(BVHLeafNodeBufferHandle, 0, Marshal.SizeOf<BVHNode>() * leafNodes.Length, leafNodes);
    }
}