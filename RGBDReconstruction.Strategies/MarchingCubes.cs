using OpenTK.Mathematics;
using RGBDReconstruction.Application;

namespace RGBDReconstruction.Strategies;

public class MarchingCubes
{
    public static Mesh GenerateMeshFromVoxelGrid(IVoxelGrid voxelGrid)
    {
        var start = new float[] { voxelGrid.XStart, voxelGrid.YStart, voxelGrid.ZStart };
        
        var uniqueVertices = new HashSet<Vertex>();
        var vertexIndex = new Dictionary<Vertex, int>();

        var positions = new List<float>();
        var normals = new List<float>();
        var texCoords = new List<float>();
        var vertexIndices = new List<int>();

        var posVertexAttribute = new VertexAttribute(0, sizeof(float), 3);
        var normVertexAttribute = new VertexAttribute(3, sizeof(float), 3);
        var texVertexAttribute = new VertexAttribute(6, sizeof(float), 2);
        
        var vertexAttribDictionary = new Dictionary<string, VertexAttribute>();
        vertexAttribDictionary["positions"] = posVertexAttribute;
        vertexAttribDictionary["normals"] = normVertexAttribute;
        vertexAttribDictionary["textureCoordinates"] = texVertexAttribute;
        
        var currentIndex = 0;

        for (int i = 0; i < voxelGrid.Size-1; i++)
        {
            for (int j = 0; j < voxelGrid.Size-1; j++)
            {
                for (int k = 0; k < voxelGrid.Size-1; k++)
                {
                    var currentVoxel = new float[]
                    {
                        start[0] + k*voxelGrid.Resolution,
                        start[1] + j*voxelGrid.Resolution,
                        start[2] + i*voxelGrid.Resolution
                    };

                    var cubeVertexConfig = GetCubeVertexConfiguration(currentVoxel, voxelGrid);

                    var localTrianglesEdgeIdxs = GetTriangleEdges(cubeVertexConfig);
                    
                    foreach (var localTriangleEdgeIdxs in localTrianglesEdgeIdxs)
                    {
                        var worldTriangleVertexPositions =
                            LinearInterpolateTriangleVertices(currentVoxel, voxelGrid, localTriangleEdgeIdxs);

                        var cubeNormals = GetVertexNormals(currentVoxel, voxelGrid);
                        var worldTriangleVertexNormals = GetInterpolatedTriangleNormals(cubeNormals, voxelGrid,
                            localTriangleEdgeIdxs, currentVoxel);

                        for (int l = 0; l < worldTriangleVertexPositions.Length; l++)
                        {
                            var pos = worldTriangleVertexPositions;
                            var norm = worldTriangleVertexNormals;
                            var position = new Vector3(pos[l][0], pos[l][1], pos[l][2]);
                            var normal = new Vector3(norm[l][0], norm[l][1], norm[l][2]);

                            // UV Coordinate is a simple xy plane projection
                            var texCoord = new Vector2(position[1], position[0]);
                            var vertex = new Vertex(position, normal, texCoord);
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

                                texCoords.Add(texCoord[0]);
                                texCoords.Add(texCoord[1]);
                            }
                        }
                    }
                }
            }
        }
        
        var meshLayout = new MeshLayout(8, vertexIndices.ToArray(), vertexAttribDictionary);

        return new Mesh(meshLayout, positions, normals, texCoords);
    }

    private static byte GetCubeVertexConfiguration(float[] currentVertexInWorldSpace, IVoxelGrid voxelGrid)
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
    private static float[][] LinearInterpolateTriangleVertices(float[] currentVertexInWorldSpace, IVoxelGrid voxelGrid, int[] edgeIdxs)
    {
        var interpolatedVertexValues = new float[edgeIdxs.Length][];
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
            
            var endVertexValue = voxelGrid[endVertex[0], endVertex[1], endVertex[2]];

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

            idx++;
        }

        return interpolatedVertexValues;
    }

    private static float[][] GetVertexNormals(float[] currentVertexInWorldSpace, IVoxelGrid voxelGrid)
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
        int[] localTriangleEdges, float[] currentVertexInWorldSpace)
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
}