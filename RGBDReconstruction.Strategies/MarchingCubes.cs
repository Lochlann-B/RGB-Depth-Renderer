﻿using Geometry;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;
using RGBDReconstruction.Strategies;

namespace RGBDReconstruction.Strategies;

public class MarchingCubes
{
    public static ColouredMesh GenerateMeshFromVoxelGrid(IVoxelGrid voxelGrid)
    {
        
        var uniqueVertices = new HashSet<ColouredVertex>();
        var vertexIndex = new Dictionary<ColouredVertex, int>();

        var positions = new List<float>();
        var normals = new List<float>();
        var vertexIndices = new List<int>();
        var colours = new List<float>();

        var posVertexAttribute = new VertexAttribute(0, sizeof(float), 3);
        var normVertexAttribute = new VertexAttribute(3, sizeof(float), 3);
        var colourVertexAttribute = new VertexAttribute(6, sizeof(float), 4);
        
        var vertexAttribDictionary = new Dictionary<string, VertexAttribute>();
        vertexAttribDictionary["positions"] = posVertexAttribute;
        vertexAttribDictionary["normals"] = normVertexAttribute;
        vertexAttribDictionary["colours"] = colourVertexAttribute;
        
        var maxY = 1080;
        var maxX = 1920;

        int cx = (maxX + 1) / 2;
        int cy = (maxY + 1) / 2;
        float fx = DepthTessellator.GetFocal(50f, 36f, 1920f);
        float fy = DepthTessellator.GetFocal(50f, 36f, 1080f);
        
        
        var currentIndex = 0;
        var blep = 2;

        foreach (var currentVoxel in GetSeenVoxels(voxelGrid)) 
        {

            var cubeVertexConfig = GetCubeVertexConfiguration(currentVoxel, voxelGrid);

            var localTrianglesEdgeIdxs = GetTriangleEdges(cubeVertexConfig);
            
            foreach (var localTriangleEdgeIdxs in localTrianglesEdgeIdxs)
            {
                var posAndColour = LinearInterpolateTriangleVertices(currentVoxel, voxelGrid, localTriangleEdgeIdxs);
                var worldTriangleVertexPositions = posAndColour.Item1;
                var triangleColours = posAndColour.Item2;
                    

                var cubeNormals = GetVertexNormals(currentVoxel, voxelGrid);
                var worldTriangleVertexNormals = GetInterpolatedTriangleNormals(cubeNormals, voxelGrid,
                    localTriangleEdgeIdxs, currentVoxel);

                for (int l = 0; l < worldTriangleVertexPositions.Length; l++)
                {
                    var pos = worldTriangleVertexPositions;
                    var norm = worldTriangleVertexNormals;
                    var position = new Vector3(pos[l][0], pos[l][1], pos[l][2]);
                    var normal = new Vector3(norm[l][0], norm[l][1], norm[l][2]);
                    var colour = triangleColours[l][0];
                    
                    var vertex = new ColouredVertex(position, normal, colour);
                    if (uniqueVertices.Contains(vertex))
                    {
                        vertexIndices.Add(vertexIndex[vertex]);
                    }
                    else
                    {
                        vertexIndex[vertex] = currentIndex;
                        uniqueVertices.Add(vertex);
                        vertexIndices.Add(vertexIndex[vertex]);
                        currentIndex++;

                        positions.Add(position[0]);
                        positions.Add(position[1]);
                        positions.Add(position[2]);

                        normals.Add(normal[0]);
                        normals.Add(normal[1]);
                        normals.Add(normal[2]);
                        
                        // TODO: Colours
                        colours.Add(colour[0]);
                        colours.Add(colour[1]);
                        colours.Add(colour[2]);
                        colours.Add(colour[3]);
                    
                        // texCoords.Add(texCoord[0]);
                        // texCoords.Add(texCoord[1]);
                    }
                    
                    // single tex coord should be sufficient?
                    
                }
                
            }
        }
        
        var meshLayout = new MeshLayout(10, vertexIndices.ToArray(), vertexAttribDictionary);

        return new ColouredMesh(meshLayout, positions, normals, colours);
    }

    private static List<Vector2> GetTexCoords(Vector3 pos, List<Matrix4> camPoses, Matrix3 intrinsicMatrix, int maxX, int maxY)
    {
        var res = new List<Vector2>();
        foreach (var pose in camPoses)
        {
            var camView = pose * (new Vector4(pos, 1.0f));
            var pixelCoords = intrinsicMatrix * camView.Xyz;

            pixelCoords /= pixelCoords.Z;

            var texCoords = pixelCoords.Xy / (new Vector2(maxX, maxY));
            
            // TODO: add weights?
            res.Add(texCoords);
        }

        return res;
    }

    private static byte GetCubeVertexConfiguration(Vector3 currentVertexInWorldSpace, IVoxelGrid voxelGrid)
    {
        byte config = 0b00000000;
        var localCubeVertexCoords = VoxelCube.Vertices;
        for(int i = 0; i < VoxelCube.Vertices.GetLength(0); i++)
        {
            var vtx = localCubeVertexCoords[i];
            config |= Convert.ToByte(Convert.ToInt16(voxelGrid[(currentVertexInWorldSpace[0] + vtx[0]*voxelGrid.Resolution), (currentVertexInWorldSpace[1] + vtx[1]*voxelGrid.Resolution),
                (currentVertexInWorldSpace[2] + vtx[2]*voxelGrid.Resolution)] < 0) << i);
        }

        return config;
    }

    private static int[][] GetTriangleEdges(byte vertexConfig)
    {
        var triangles = new List<int[]>();
        var triangleVerticesAsEdges = MarchingCubeEdgeTriangulations.Configurations[vertexConfig];
        for (int i = 0; i < triangleVerticesAsEdges.Length; i += 3)
        {
            if (triangleVerticesAsEdges[i] < 0 || triangleVerticesAsEdges[i + 1] < 0 ||
                triangleVerticesAsEdges[i + 2] < 0)
            {
                break;
            }

            triangles.Add([triangleVerticesAsEdges[i],
                triangleVerticesAsEdges[i+1],
                triangleVerticesAsEdges[i+2]]);
        }

        return triangles.ToArray();
    }

    // These guys are done per-triangle
    private static (float[][],Vector4[][]) LinearInterpolateTriangleVertices(Vector3 currentVertexInWorldSpace, IVoxelGrid voxelGrid, int[] edgeIdxs)
    {
        var interpolatedVertexValues = new float[edgeIdxs.Length][];
        var interpolatedColourValues = new Vector4[edgeIdxs.Length][];
        var idx = 0;
        foreach (var edgeIdx in edgeIdxs)
        {
            var localCubeIdxs = VoxelCube.Edges[edgeIdx];
            var startLocalCubeCoord = VoxelCube.Vertices[localCubeIdxs[0]];
            var endLocalCubeCoord = VoxelCube.Vertices[localCubeIdxs[1]];

            var startVertex = new float[] {
                currentVertexInWorldSpace[0] + voxelGrid.Resolution * startLocalCubeCoord[0],
                currentVertexInWorldSpace[1] + voxelGrid.Resolution * startLocalCubeCoord[1],
                currentVertexInWorldSpace[2] + voxelGrid.Resolution * startLocalCubeCoord[2]
            };

            var endVertex = new float[]
            {
                currentVertexInWorldSpace[0] + voxelGrid.Resolution * endLocalCubeCoord[0],
                currentVertexInWorldSpace[1] + voxelGrid.Resolution * endLocalCubeCoord[1],
                currentVertexInWorldSpace[2] + voxelGrid.Resolution * endLocalCubeCoord[2]
            };

            var startVertexValue = voxelGrid[startVertex[0], startVertex[1], startVertex[2]];
            var startColourValue = voxelGrid.GetColour(startVertex[0], startVertex[1], startVertex[2]);
            // var startColourValue = new Vector4((startVertex[0] + 2) / 2f, (startVertex[1] + 2) / 3f,
            //     (startVertex[2] + 0.25f) / 6f, 1.0f);
            
            var endVertexValue = voxelGrid[endVertex[0], endVertex[1], endVertex[2]];
            var endColourValue = voxelGrid.GetColour(endVertex[0], endVertex[1], endVertex[2]);
            // var endColourValue = new Vector4((endVertex[0] + 2) / 2f, (endVertex[1] + 2) / 3f,
            //     (endVertex[2] + 0.25f) / 6f, 1.0f);
            
            
            var proportion = -startVertexValue / (endVertexValue - startVertexValue);
            interpolatedVertexValues[idx] =
            [
                startVertex[0] +
                proportion *
                (endVertex[0] - startVertex[0]), //voxelGrid.Resolution * proportion * endLocalCubeCoord[0],
                startVertex[1] +
                proportion *
                (endVertex[1] - startVertex[1]), //voxelGrid.Resolution * proportion * endLocalCubeCoord[1],
                startVertex[2] + proportion * (endVertex[2] - startVertex[2])
            ];//voxelGrid.Resolution * proportion * endLocalCubeCoord[2]];

            interpolatedColourValues[idx] = 
            [
                startColourValue +
                proportion *
                (endColourValue - startColourValue), //voxelGrid.Resolution * proportion * endLocalCubeCoord[0],
            ];

            idx++;
        }

        return (interpolatedVertexValues, interpolatedColourValues);
    }

    private static float[][] GetVertexNormals(Vector3 currentVertexInWorldSpace, IVoxelGrid voxelGrid)
    {
        var cV = currentVertexInWorldSpace;
        var res = voxelGrid.Resolution;
        var normals = new float[VoxelCube.Vertices.Length][];
        for (int i = 0; i < VoxelCube.Vertices.Length; i++)
        {
            var vtx = VoxelCube.Vertices[i];
            float x = cV[0] + vtx[0] * res;
            float y = cV[1] + vtx[1] * res;
            float z = cV[2] + vtx[2] * res;

            float xi = Math.Min(voxelGrid.XStart + (voxelGrid.Size-1) * res, x+res);
            float yi = Math.Min(voxelGrid.YStart + (voxelGrid.Size-1) * res, y+res);
            float zi = Math.Min(voxelGrid.ZStart + (voxelGrid.Size-1) * res, z+res);
            float xd = Math.Max(voxelGrid.XStart, x-res);
            float yd = Math.Max(voxelGrid.YStart, y-res);
            float zd = Math.Max(voxelGrid.ZStart, z-res);
            normals[i] = [
                voxelGrid[xi,y,z] - voxelGrid[xd, y, z],
                voxelGrid[x,yi,z] - voxelGrid[x, yd, z],
                voxelGrid[x,y,zi] - voxelGrid[x, y, zd]
            ];
        }

        return normals;
    }

    private static float[][] GetInterpolatedTriangleNormals(float[][] cubeVertexNormals, IVoxelGrid voxelGrid,
        int[] localTriangleEdges, Vector3 currentVertexInWorldSpace)
    {
        var interpolatedNormals = new float[localTriangleEdges.Length][];
        var curVtx = currentVertexInWorldSpace;
        var res = voxelGrid.Resolution;
        for (int i = 0; i < localTriangleEdges.Length; i++)
        {
            // Indices of cubeVertex normals correspond to indices of local cube vertices
            var startNormal = cubeVertexNormals[VoxelCube.Edges[localTriangleEdges[i]][0]];
            var endNormal = cubeVertexNormals[VoxelCube.Edges[localTriangleEdges[i]][1]];

            var startVtxIdxs = VoxelCube.Vertices[VoxelCube.Edges[localTriangleEdges[i]][0]];
            var endVtxIdxs = VoxelCube.Vertices[VoxelCube.Edges[localTriangleEdges[i]][1]];
            var startVtx = voxelGrid[curVtx[0]+res*startVtxIdxs[0], curVtx[1]+res*startVtxIdxs[1], curVtx[2]+res*startVtxIdxs[2]];
            var endVtx = voxelGrid[curVtx[0]+res*endVtxIdxs[0], curVtx[1]+res*endVtxIdxs[1], curVtx[2]+res*endVtxIdxs[2]];

            var proportion = -startVtx / (endVtx - startVtx);

            var interpNormal = new float[]
            {
                startNormal[0]*proportion + (1-proportion)*endNormal[0],
                startNormal[1]*proportion + (1-proportion)*endNormal[1],
                startNormal[2]*proportion + (1-proportion)*endNormal[2],
            };

            interpolatedNormals[i] = interpNormal;
        }

        return interpolatedNormals;
    }

    protected static List<Vector3> GetAllVoxels(IVoxelGrid voxelGrid)
    {
        var start = new float[] { voxelGrid.XStart, voxelGrid.YStart, voxelGrid.ZStart };
        var voxelList = new List<Vector3>();
        for (int i = 0; i < voxelGrid.Size - 1; i++)
        {
            for (int j = 0; j < voxelGrid.Size - 1; j++)
            {
                for (int k = 0; k < voxelGrid.Size - 1; k++)
                {
                    voxelList.Add(new Vector3(start[0] + k * voxelGrid.Resolution,
                        start[1] + j * voxelGrid.Resolution,
                        start[2] + i * voxelGrid.Resolution));
                }
            }
        }

        return voxelList;
    }

    protected static List<Vector3> GetSeenVoxels(IVoxelGrid voxelGrid)
    {
        return voxelGrid.SeenVoxels;
    }
}