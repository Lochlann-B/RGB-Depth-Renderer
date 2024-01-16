using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;

namespace RGBDReconstruction.Strategies;

public class DepthTessellator
{

    public static Mesh TessellateDepthArray(float[,] depthMap)
    {
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

        var maxY = depthMap.GetLength(0)-1;
        var maxX = depthMap.GetLength(1)-1;

        int cx = (maxX + 1) / 2;
        int cy = (maxY + 1) / 2;
        float fx = GetFocal(50f, 36f, 1920f);
        float fy = GetFocal(28.125f, 36*(9/16f), 1080f);

        for (int y = 0; y < maxY; y++)
        {
            for (int x = 0; x < maxX; x++)
            {
                // Vertices start in the bottom right, and walk anticlockwise around the quad
                var vertex1 = new Vector3( depthMap[y,x]*((x-cx)/fx), depthMap[y,x]*((y-cy)/fy), depthMap[y,x]);
                var vertex2 = new Vector3( depthMap[y,x+1]*((x+1-cx)/fx), depthMap[y,x+1]*((y-cy)/fy), depthMap[y,x+1]);
                var vertex3 = new Vector3( depthMap[y+1,x+1]*((x+1-cx)/fx), depthMap[y+1,x+1]*((y+1-cy)/fy), depthMap[y+1,x+1] );
                var vertex4 = new Vector3( depthMap[y+1,x]*((x-cx)/fx), depthMap[y+1,x]*((y+1-cy)/fy), depthMap[y+1,x] );

                var triangle1 = new Triangle(vertex1, vertex2, vertex3);
                var triangle2 = new Triangle(vertex1, vertex3, vertex4);

                Triangle[] triangles = [triangle1, triangle2];

                foreach (Triangle triangle in triangles)
                {
                    if (triangle.HasLengthLongerThanThreshold(0.1f))
                    {
                        continue;
                    }
                    var normal = triangle.GetNormal();
                    foreach (var position in triangle.GetVerticesAsList())
                    {
                        // UV Coordinate is a simple xy plane projection
                        var texCoord = new Vector2((fx*position[1]/position[2] + cx)/maxX, ((fy*position[0]/position[2] + cy))/maxY);
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
        
        var meshLayout = new MeshLayout(8, vertexIndices.ToArray(), vertexAttribDictionary);

        return new Mesh(meshLayout, positions, normals, texCoords);
    }

    private static float GetFocal(float focalLength, float sensorSize, float imgSize)
    {
        return focalLength * (imgSize / sensorSize);
    }

}