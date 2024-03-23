using System.Diagnostics;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
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

    public static BVHNode[] GetBVH(float[] positionArray, int[] indexArray, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        var unsortedTriangleIdxArray = Enumerable.Range(0, indexArray.Length / 3).ToArray();
        
        var (sortedMortonCodes, sortedObjIdxs) = SortMortonCodesComputeShader(positionArray, indexArray, unsortedTriangleIdxArray, xRanges, yRanges, zRanges);

        return GenerateHierarchy(sortedMortonCodes, sortedObjIdxs, sortedObjIdxs.Length);
    }

    private static (uint[], int[]) SortMortonCodesComputeShader(float[] positionArray, int[] indexArray, int[] unsortedIdxArray, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        var mortonCodes = GetMortonCodesComputeShader(indexArray, positionArray, xRanges, yRanges, zRanges);
        var watch = Stopwatch.StartNew();

        var (sortedMortonCodes, sortedIdxArray) = GPUParallelRadixSort(mortonCodes, unsortedIdxArray);
        // for (int i = 0; i < mortonCodesCopy.Length; i++)
        // {
        //     int originalIdx = Array.IndexOf(mortonCodes, mortonCodesCopy[i]);
        //     sortedIdxArray[i] = unsortedIdxArray[originalIdx];
        // }
        watch.Stop();
        Console.WriteLine("Time taken to sort morton codes: {0}ms", watch.ElapsedMilliseconds);
        watch.Reset();

        return (sortedMortonCodes, sortedIdxArray);
    }

    private static (uint[], int[]) GPUParallelRadixSort(uint[] values, int[] indexes)
    {
        // Code adapted from the Samples section of the ILGPU library, under the University of Illinois/NCSA Open Source license:
        // https://spdx.org/licenses/NCSA.html
        
         // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            var cudaDevices = context.GetCudaDevices();

            var device = context.GetPreferredDevice(preferCPU: false);
            
            if (cudaDevices.Count > 0)
            {
                // Too bad if you have several CUDA GPUs and the first one sucks!
                device = cudaDevices[0];
            }
            
            // Create the associated accelerator
            using var accelerator = device.CreateAccelerator(context);
            Console.WriteLine($"Performing operations on {accelerator}");
            
            // Allocate the source buffer that will be sorted later on.
            

            //var zippedArray = values.Zip(indexes, (k, v) => (k,v)).ToArray();
            
            //using var sourceBuffer = accelerator.Allocate1D<(uint, int)>(values.Length);

            using var sourceBuffer = accelerator.Allocate1D<uint>(values.Length);
            
            sourceBuffer.CopyFromCPU(values);

            using var valuesBuffer = accelerator.Allocate1D<int>(indexes.Length);
            valuesBuffer.CopyFromCPU(indexes);

            var radixSort = accelerator.CreateRadixSortPairs<uint, Stride1D.Dense, int, Stride1D.Dense, AscendingUInt32>();

            // The parallel scan implementation needs temporary storage.
            // By default, every accelerator hosts a memory-buffer cache
            // for operations that require a temporary cache.

            // Create a new radix sort instance using a descending int sorting.
            //var radixSort = accelerator.CreateRadixSort<uint, Stride1D.Dense, AscendingUInt32>();

            // Compute the required amount of temporary memory
        
            var tempMemSize = accelerator.ComputeRadixSortPairsTempStorageSize<uint, int, AscendingUInt32>((Index1D)(sourceBuffer.Length));
            using (var tempBuffer = accelerator.Allocate1D<int>(tempMemSize))
            {
                // Performs a descending radix-sort operation
                radixSort(
                    accelerator.DefaultStream,
                    sourceBuffer.View,
                    valuesBuffer.View,
                    tempBuffer.View);
            }

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            var sortedMortonCodes = sourceBuffer.GetAsArray1D();
            var sortedIdxs = valuesBuffer.GetAsArray1D();
            

            return (sortedMortonCodes, sortedIdxs);
    }

    private static uint[] GetMortonCodesComputeShader(int[] indexBuffer, float[] positionBuffer, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        var mortonCodeData = new uint[indexBuffer.Length/3];
        Console.WriteLine(GL.GetError());
            
        // int indexBufferTexture = GL.GenTexture();
        // GL.BindTexture(TextureTarget.Texture1D, indexBufferTexture);
        // GL.TexStorage1D(TextureTarget1d.Texture1D, 1, SizedInternalFormat.R32i, indexBuffer.Length);
        // Console.WriteLine(GL.GetError());
        // GL.TexSubImage1D(TextureTarget.Texture1D, 0, 0, indexBuffer.Length, PixelFormat.RedInteger, PixelType.Int, indexBuffer);
        // Console.WriteLine("index buffer texture upload errors: ");
        // Console.WriteLine(GL.GetError());
        //
        // int positionBufferTexture = GL.GenTexture();
        // GL.BindTexture(TextureTarget.Texture1D, positionBufferTexture);
        // GL.TexStorage1D(TextureTarget1d.Texture1D, 1, SizedInternalFormat.R32f, positionBuffer.Length);
        // GL.TexSubImage1D(TextureTarget.Texture1D, 0, 0, positionBuffer.Length, PixelFormat.Red, PixelType.Float, positionBuffer);
        // Console.WriteLine("position buffer texture upload errors: ");
        // Console.WriteLine(GL.GetError());

        
        
        var computeShader = new ComputeShader("./ComputeShaders/MortonCodeComputeShader.glsl");
    
        computeShader.Use();
        
        computeShader.SetUniformFloatArray("xRanges", ref xRanges);
        computeShader.SetUniformFloatArray("yRanges", ref yRanges);
        computeShader.SetUniformFloatArray("zRanges", ref zRanges);
    
        // GL.BindImageTexture(1, indexBufferTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32i);
        // GL.BindImageTexture(2, positionBufferTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
    
        // Generate buffer handles
        var mortonCodeBufferHandle = GL.GenBuffer();
    
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mortonCodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(uint)*mortonCodeData.Length, mortonCodeData, BufferUsageHint.StaticDraw);
    
        // Bind the SSBOs to read their data
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, mortonCodeBufferHandle);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int indexBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, indexBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int)*indexBuffer.Length, indexBuffer, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, 1, indexBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        
        int positionBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, positionBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float)*positionBuffer.Length, positionBuffer, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer, 2, positionBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        
        
        
        watch.Stop();
        Console.WriteLine("Morton code compute shader compile time: {0}ms", watch.ElapsedMilliseconds);

        watch.Reset();
        watch.Start();
        GL.DispatchCompute(mortonCodeData.Length, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        watch.Stop();
        Console.WriteLine("Morton code generation time: {0}ms", watch.ElapsedMilliseconds);
        
    
        // read the data
        GL.GetNamedBufferSubData(mortonCodeBufferHandle, 0, sizeof(uint) * mortonCodeData.Length, mortonCodeData);
        
        //return new uint[10];
        return mortonCodeData;
    }

    private static void BVHGenerationComputeShader(BVHNode[] internalNodes, BVHNode[] leafNodes, int numObjs, uint[] sortedMortonCodes)
    {
         var computeShader = new ComputeShader("./ComputeShaders/BVHComputeShader.glsl");
        
        computeShader.Use();
        
        computeShader.SetUniformInt("numObjs", numObjs);
        
        // Generate buffer handles
        int BVHInternalNodeBufferHandle = GL.GenBuffer();
        int BVHLeafNodeBufferHandle = GL.GenBuffer();
        int sortedMortonCodesBufferHandle = GL.GenBuffer();
        
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHInternalNodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>()*internalNodes.Length, internalNodes, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHLeafNodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>() * leafNodes.Length, leafNodes, BufferUsageHint.StaticDraw);
        
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, sortedMortonCodesBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(uint) * sortedMortonCodes.Length, sortedMortonCodes, BufferUsageHint.StaticRead);
        
        // Bind the SSBOs to read their data
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, BVHInternalNodeBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, BVHLeafNodeBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, sortedMortonCodesBufferHandle);
        
        
        
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