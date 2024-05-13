using System.Diagnostics;
using System.Numerics;
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
    private static BVHNode[] GenerateHierarchy(ulong[] sortedMortonCodes, int[] sortedObjIDs, int numObjects, float[] positionArray, int[] indexArray)
    {
        var leafNodes = new BVHNode[numObjects];
        var internalNodes = new BVHNode[numObjects - 1];
        var blep = new Vector3();
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
        
        genHierarchyCPU(internalNodes, leafNodes, sortedMortonCodes, leafNodes.Length);
        
       
        
        AssignBoundingBoxes(internalNodes, leafNodes, sortedObjIDs, indexArray, positionArray);
        
        // TODO: Return the list of nodes given by each Compute Shader
        var BVHNodes = new BVHNode[2 * numObjects - 1];
        internalNodes.CopyTo(BVHNodes, 0);
        leafNodes.CopyTo(BVHNodes, numObjects-1);

        return BVHNodes;
    }

    public static BVHNode[] GetBVH(float[] positionArray, int[] indexArray, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        //var unsortedTriangleIdxArray = Enumerable.Range(0, indexArray.Length / 3).ToArray();

        //morton(positionArray, xRanges, yRanges, zRanges, indexArray);
        
        var (sortedMortonCodes, sortedObjIdxs) = SortMortonCodesComputeShader(positionArray, indexArray, xRanges, yRanges, zRanges);

        return GenerateHierarchy(sortedMortonCodes, sortedObjIdxs, sortedObjIdxs.Length, positionArray, indexArray);
    }

    public static void genHierarchyCPU(BVHNode[] internalNodes, BVHNode[] leafNodes, ulong[] sortedMortonCodes, int numObjs)
    {
        // in parallel
        for (int idx = 0; idx < internalNodes.Length; idx++)
        {
            var range = determineRange(sortedMortonCodes, idx, numObjs);
    
            int split = findSplit(sortedMortonCodes, (int)range[0], (int)range[1]);
    
            BVHNode childA;
            BVHNode childB;
    
            int chAIdx;
            int chBIdx;
    
            if (split == range[0])
            {
                childA = leafNodes[split];
                chAIdx = split + (numObjs - 1);
            }
            else
            {
                childA = internalNodes[split];
                chAIdx = split;
            }
    
            if (split + 1 == range[1])
            {
                childB = leafNodes[split + 1];
                chBIdx = split + numObjs;
            }
            else
            {
                childB = internalNodes[split + 1];
                chBIdx = split + 1;
            }
    
            BVHNode currNode = internalNodes[idx];
            currNode.leftIdx = (uint)chAIdx;
            currNode.rightIdx = (uint)chBIdx;
    
            internalNodes[idx] = currNode;
    
            childA.parentIdx = (uint)idx;
            if (chAIdx >= numObjs - 1)
            {
                leafNodes[chAIdx - (numObjs - 1)] = childA;
            }
            else
            {
                internalNodes[chAIdx] = childA;
            }
    
            childB.parentIdx = (uint)idx;
            if (chBIdx >= numObjs - 1)
            {
                leafNodes[chBIdx - (numObjs - 1)] = childB;
            }
            else
            {
                internalNodes[chBIdx] = childB;
            }
        }
    }

    public static int findSplit(ulong[] sortedMortonCodes, int first, int last)
    {
        ulong firstCode = sortedMortonCodes[first];
        ulong lastCode = sortedMortonCodes[last];
    
        if (firstCode == lastCode)
        {
            return first;
        }
    
        int commonPrefix = clz(firstCode ^ lastCode);
    
        int split = first;
        int step = last - first;
    
        bool dowhileflag = true;
        while (dowhileflag || step > 1)
        {
            dowhileflag = false;
    
            step = (step + 1) >> 1;
            int newSplit = split + step;
    
            if (newSplit < last)
            {
                ulong splitCode = sortedMortonCodes[newSplit];
                int splitPrefix = clz(firstCode ^ splitCode);
                if (splitPrefix > commonPrefix)
                {
                    split = newSplit;
                }
            }
        }
    
        return split;
    }

    public static Vector2 determineRange(ulong[] sortedMortonCodes, int idx, int numObjs)
    {
        int numIn = numObjs - 1;
    
        if (idx == 0)
        {
            return new Vector2(0, numIn);
        }
    
        int dir;
    
        int delMin;
        int initialIdx = idx;
    
        ulong prevCode = sortedMortonCodes[idx - 1];
        ulong currCode = sortedMortonCodes[idx];
        ulong nextCode = sortedMortonCodes[idx + 1];
    
        if (prevCode == currCode && nextCode == currCode)
        {
            int UB = numIn - 1;
            int LB = idx;
            ulong originalCode = currCode;
            while (idx > 0 && idx < numIn)
            {
                idx = (UB + LB) / 2;
                if (idx >= numIn)
                {
                    break;
                }
    
                if (sortedMortonCodes[idx] != originalCode && sortedMortonCodes[idx + 1] != originalCode)
                {
                    // gone too high, reduce upper bound
                    UB = idx;
                }
    
                if (sortedMortonCodes[idx] == originalCode && sortedMortonCodes[idx + 1] == originalCode)
                {
                    // gone too low, increase lower bound
                    LB = idx;
                }
    
                if (sortedMortonCodes[idx] == originalCode && originalCode != sortedMortonCodes[idx + 1])
                {
                    break;
                }
            }
    
            return new Vector2(initialIdx, idx);
        }
        else
        {
            var left = clz(currCode ^ prevCode);
            var right = clz(currCode ^ nextCode);
    
            if (left > right)
            {
                dir = -1;
                delMin = right;
            }
            else
            {
                dir = 1;
                delMin = left;
            }
        }
    
        int lMax = 2;
        int testIdx = idx + lMax * dir;
    
        while (testIdx <= numIn && testIdx >= 0 && (clz(currCode ^ sortedMortonCodes[testIdx]) > delMin))
        {
            lMax *= 2;
            testIdx = idx + lMax * dir;
        }
    
        int l = 0;
    
        for (int div = 2; lMax / div >= 1; div *= 2)
        {
            int t = lMax / div;
            int newTest = idx + (l + t) * dir;
    
            if (newTest <= numIn && newTest >= 0)
            {
                int splitPrefix = clz(currCode ^ sortedMortonCodes[newTest]);
    
                if (splitPrefix > delMin)
                {
                    l = l + t;
                }
            }
        }
    
        if (dir == 1)
        {
            return new Vector2(idx, idx + l * dir);
        }
        else
        {
            return new Vector2(idx + l * dir, idx);
        }
    }
    
static Vector3 findCentroid(Vector3 v1, Vector3 v2, Vector3 v3) {
    //return (v1 + v2 + v3)/3;
    float maxX = Math.Max(v1[0], Math.Max(v2[0], v3[0]));
    float minX = Math.Min(v1[0], Math.Min(v2[0], v3[0]));
    float maxY = Math.Max(v1[1], Math.Max(v2[1], v3[1]));
    float minY = Math.Min(v1[1], Math.Min(v2[1], v3[1]));
    float maxZ = Math.Max(v1[2], Math.Max(v2[2], v3[2]));
    float minZ = Math.Min(v1[2], Math.Min(v2[2], v3[2]));
    
    return new Vector3((maxX + minX)/2f, (maxY + minY)/2f, (maxZ + minZ)/2f);
}

static uint expandBits(uint v) {
    v = (v * 0x00010001u) & 0xFF0000FFu;
    v = (v * 0x00000101u) & 0x0F00F00Fu;
    v = (v * 0x00000011u) & 0xC30C30C3u;
    v = (v * 0x00000005u) & 0x49249249u;
    return v;
}

static uint getMortonCode(Vector3 p) {
    float x = p[0];
    float y = p[1];
    float z = p[2];
    x = Math.Min(Math.Max(x * 1024.0f, 0.0f), 1023.0f);
    y = Math.Min(Math.Max(y * 1024.0f, 0.0f), 1023.0f);
    z = Math.Min(Math.Max(z * 1024.0f, 0.0f), 1023.0f);
    uint xx = expandBits((uint)(x));
    uint yy = expandBits((uint)(y));
    uint zz = expandBits((uint)(z));
    return xx * 4 + yy * 2 + zz;
}

static void morton(float[] positionBufferArray, float[] xRanges, float[] yRanges, float[] zRanges, int[] indexBufferArray)
{
    var mortonCodes = new uint[indexBufferArray.Length / 3];

    for (int i = 0; i < mortonCodes.Length; i++)
    {

        int i1 = (int)(3 * i);
        int i2 = i1 + 1;
        int i3 = i1 + 2;

        var minCoords = new Vector3(xRanges[0], yRanges[0], zRanges[0]);
        var maxCoords = new Vector3(xRanges[1], yRanges[1], zRanges[1]);

        var v1 = new Vector3(positionBufferArray[3 * indexBufferArray[i1]],
            positionBufferArray[3 * indexBufferArray[i1] + 1],
            positionBufferArray[3 * indexBufferArray[i1] + 2]);


        var v2 = new Vector3(positionBufferArray[3 * indexBufferArray[i2]],
            positionBufferArray[3 * indexBufferArray[i2] + 1],
            positionBufferArray[3 * indexBufferArray[i2] + 2]);


        var v3 = new Vector3(positionBufferArray[3 * indexBufferArray[i3]],
            positionBufferArray[3 * indexBufferArray[i3] + 1],
            positionBufferArray[3 * indexBufferArray[i3] + 2]);



        var centre = findCentroid(v1, v2, v3);

        centre = (centre - minCoords) / (maxCoords - minCoords);

        mortonCodes[i] = getMortonCode(centre);
    }
}

    static void AABB(BVHNode[] BVHInternals, BVHNode[] BVHLeaves, int[] idxArray, float[] positionArray, int numObjs)
    {
        for (int idx = 0; idx < BVHLeaves.Length; idx++)
        {

            int currentIdx = idx;
            var currentNode = BVHLeaves[idx];
            uint currentParentIdx = currentNode.parentIdx;

            uint triIdx = currentNode.objID;

            int v1Idx = idxArray[3 * triIdx];
            int v2Idx = idxArray[3 * triIdx + 1];
            int v3Idx = idxArray[3 * triIdx + 2];

            Vector3 v1 = new Vector3(positionArray[3 * v1Idx], positionArray[3 * v1Idx + 1],
                positionArray[3 * v1Idx + 2]);
            Vector3 v2 = new Vector3(positionArray[3 * v2Idx], positionArray[3 * v2Idx + 1],
                positionArray[3 * v2Idx + 2]);
            Vector3 v3 = new Vector3(positionArray[3 * v3Idx], positionArray[3 * v3Idx + 1],
                positionArray[3 * v3Idx + 2]);

            float minX = Math.Min(v1[0], Math.Min(v2[0], v3[0]));
            float maxX = Math.Max(v1[0], Math.Max(v2[0], v3[0]));

            float minY = Math.Min(v1[1], Math.Min(v2[1], v3[1]));
            float maxY = Math.Max(v1[1], Math.Max(v2[1], v3[1]));

            float minZ = Math.Min(v1[2], Math.Min(v2[2], v3[2]));
            float maxZ = Math.Max(v1[2], Math.Max(v2[2], v3[2]));

            currentNode.maxPoint = new Vector4(maxX, maxY, maxZ, 1.0f);
            currentNode.minPoint = new Vector4(minX, minY, minZ, 1.0f);

            BVHLeaves[currentIdx] = currentNode;
            var numIters = 0;

            currentIdx = (int) currentNode.parentIdx;

            while (!(currentIdx == 0 && currentParentIdx == 0))
            {

                if (numIters > 100)
                {
                    var blep = 0;
                    break;
                }

                currentNode = BVHInternals[currentIdx];
                currentParentIdx = currentNode.parentIdx;

                BVHNode left;
                if (currentNode.leftIdx >= numObjs - 1)
                {
                    left = BVHLeaves[currentNode.leftIdx - numObjs + 1];
                }
                else
                {
                    left = BVHInternals[currentNode.leftIdx];
                }

                BVHNode right;
                if (currentNode.rightIdx >= numObjs - 1)
                {
                    right = BVHLeaves[currentNode.rightIdx - numObjs + 1];
                }
                else
                {
                    right = BVHInternals[currentNode.rightIdx];
                }

                var maxLeft = left.maxPoint;
                var maxRight = right.maxPoint;

                var minLeft = left.minPoint;
                var minRight = right.minPoint;

                Vector4 maxPoint = new Vector4(Math.Max(maxLeft[0], maxRight[0]), Math.Max(maxLeft[1], maxRight[1]),
                    Math.Max(maxLeft[2], maxRight[2]), 1.0f);
                Vector4 minPoint = new Vector4(Math.Min(minLeft[0], minRight[0]), Math.Min(minLeft[1], minRight[1]),
                    Math.Min(minLeft[2], minRight[2]), 1.0f);

                currentNode.maxPoint = maxPoint;
                currentNode.minPoint = minPoint;

                BVHInternals[currentIdx] = currentNode;

                
                numIters++;

                currentIdx = (int)currentParentIdx;
                currentParentIdx = BVHInternals[currentIdx].parentIdx;
            }
        }
        
        
    }

    private static void AssignBoundingBoxes(BVHNode[] internalNodes, BVHNode[] leafNodes, int[] sortedObjIdxs,
        int[] idxArray, float[] positionArray)
    {
        // Parallel bottom-up reduction compute shader:
        // Operate on each leaf node
        // Have an atomic counter buffer for each internal node
        // When atomic counter = 0, increment the counter and terminate the thread.
        // Otherwise, update the node's bounding box by taking the union of its children's bounding boxes.
        
        // For a leaf node, AABB is determined by the min/maxes of the triangle of that object.

        var watch = Stopwatch.StartNew();

        var computeShader = new ComputeShader("./ComputeShaders/BoundingBoxComputeShader.glsl");
        
        computeShader.Use();
        
        computeShader.SetUniformInt("numObjs", sortedObjIdxs.Length);
        
        int internalNodesBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, internalNodesBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>()*internalNodes.Length, internalNodes, BufferUsageHint.StaticDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, internalNodesBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int leafNodesBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, leafNodesBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<BVHNode>()*leafNodes.Length, leafNodes, BufferUsageHint.StaticDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, leafNodesBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int sortedObjsBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, sortedObjsBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int)*sortedObjIdxs.Length, sortedObjIdxs, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, sortedObjsBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int indexBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, indexBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int)*idxArray.Length, idxArray, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, indexBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        
        int positionBufferSSBO = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, positionBufferSSBO);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float)*positionArray.Length, positionArray, BufferUsageHint.StaticRead);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 4, positionBufferSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        
        watch.Stop();
        Console.WriteLine("Time taken for AABB compute shader compilation: {0}ms", watch.ElapsedMilliseconds);
        watch.Reset();
        
        watch.Start();
        GL.DispatchCompute(leafNodes.Length, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        watch.Stop();
        Console.WriteLine("AABB Calculation time: {0}ms", watch.ElapsedMilliseconds);
        watch.Reset();
        
        // Might need memory barrier here?
        
        // read the data
        GL.GetNamedBufferSubData(internalNodesBufferSSBO, 0, Marshal.SizeOf<BVHNode>() * internalNodes.Length, ref internalNodes[0]);
        GL.GetNamedBufferSubData(leafNodesBufferSSBO, 0, Marshal.SizeOf<BVHNode>() * leafNodes.Length, ref leafNodes[0]);
        
        computeShader.Dispose();
        GL.Flush();

        var currentNode = internalNodes[0];
        var numObjs = leafNodes.Length;

        BVHNode left;
        if (currentNode.leftIdx >= numObjs - 1) {
            left = leafNodes[currentNode.leftIdx - numObjs + 1];
        } else {
            left = internalNodes[currentNode.leftIdx];
        }

        BVHNode right;
        if (currentNode.rightIdx >= numObjs - 1) {
            right = leafNodes[currentNode.rightIdx - numObjs + 1];
        } else {
            right = internalNodes[currentNode.rightIdx];
        }
        
        var maxLeft = left.maxPoint;
        var maxRight = right.maxPoint;

        var minLeft = left.minPoint;
        var minRight = right.minPoint;
        
        var maxPoint = new Vector4(Math.Max(maxLeft[0], maxRight[0]), Math.Max(maxLeft[1], maxRight[1]), Math.Max(maxLeft[2], maxRight[2]), 1.0f);
        var minPoint = new Vector4(Math.Min(minLeft[0], minRight[0]), Math.Min(minLeft[1], minRight[1]), Math.Min(minLeft[2], minRight[2]), 1.0f);
        
        currentNode.maxPoint = maxPoint;
        currentNode.minPoint = minPoint;
        
        internalNodes[0] = currentNode;
    }
    
    static int clz(ulong x)
    {
        return IntrinsicMath.BitOperations.LeadingZeroCount(x);
    }



    private static (ulong[], int[]) SortMortonCodesComputeShader(float[] positionArray, int[] indexArray, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        var mortonCodes = GetMortonCodesComputeShader(indexArray, positionArray, xRanges, yRanges, zRanges);

        var watch = Stopwatch.StartNew();
        
        var unsortedIdxArray = Enumerable.Range(0, mortonCodes.Length).ToArray();

        var (sortedMortonCodes, sortedIdxArray) = GPUParallelRadixSort(mortonCodes, unsortedIdxArray);

        watch.Stop();
        Console.WriteLine("Time taken to sort morton codes: {0}ms", watch.ElapsedMilliseconds);
        watch.Reset();

        return (sortedMortonCodes, sortedIdxArray);
    }

    private static (ulong[], int[]) GPUParallelRadixSort(ulong[] values, int[] indexes)
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

            using var sourceBuffer = accelerator.Allocate1D<ulong>(values.Length);
            
            sourceBuffer.CopyFromCPU(values);

            using var valuesBuffer = accelerator.Allocate1D<int>(indexes.Length);
            valuesBuffer.CopyFromCPU(indexes);

            var radixSort = accelerator.CreateRadixSortPairs<ulong, Stride1D.Dense, int, Stride1D.Dense, AscendingUInt64>();

            // The parallel scan implementation needs temporary storage.
            // By default, every accelerator hosts a memory-buffer cache
            // for operations that require a temporary cache.

            // Create a new radix sort instance using a descending int sorting.
            //var radixSort = accelerator.CreateRadixSort<uint, Stride1D.Dense, AscendingUInt32>();

            // Compute the required amount of temporary memory
        
            var tempMemSize = accelerator.ComputeRadixSortPairsTempStorageSize<ulong, int, AscendingUInt64>((Index1D)(sourceBuffer.Length));
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

    private static ulong[] GetMortonCodesComputeShader(int[] indexBuffer, float[] positionBuffer, float[] xRanges, float[] yRanges, float[] zRanges)
    {
        var mortonCodeData = new ulong[indexBuffer.Length/3];
        Console.WriteLine(GL.GetError());
        
        var computeShader = new ComputeShader("./ComputeShaders/MortonCodeComputeShader.glsl");
    
        computeShader.Use();
        
        computeShader.SetUniformFloatArray("xRanges", ref xRanges);
        computeShader.SetUniformFloatArray("yRanges", ref yRanges);
        computeShader.SetUniformFloatArray("zRanges", ref zRanges);
    
        // Generate buffer handles
        var mortonCodeBufferHandle = GL.GenBuffer();
    
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mortonCodeBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(ulong)*mortonCodeData.Length, mortonCodeData, BufferUsageHint.StaticDraw);
    
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
        GL.GetNamedBufferSubData(mortonCodeBufferHandle, 0, sizeof(ulong) * mortonCodeData.Length, mortonCodeData);
        Console.WriteLine(GL.GetError());
        
        computeShader.Dispose();
        
        return mortonCodeData;
    }

    private static void BVHGenerationComputeShader(BVHNode[] internalNodes, BVHNode[] leafNodes, int numObjs, ulong[] sortedMortonCodes)
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
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(ulong) * sortedMortonCodes.Length, sortedMortonCodes, BufferUsageHint.StaticRead);
        
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
        GL.GetNamedBufferSubData(BVHInternalNodeBufferHandle, 0, Marshal.SizeOf<BVHNode>() * internalNodes.Length, ref internalNodes[0]);
        GL.GetNamedBufferSubData(BVHLeafNodeBufferHandle, 0, Marshal.SizeOf<BVHNode>() * leafNodes.Length, ref leafNodes[0]);
        
        computeShader.Dispose();
    }
}