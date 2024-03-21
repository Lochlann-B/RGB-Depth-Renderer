﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;
using Geometry;

namespace RGBDReconstruction.Strategies;

public class DepthTessellator
{
    
    public static Mesh nnTessellateDepthArray(float[,] depthMap)
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

        int xres = 4;
        int yres = 4;

        var maxY = depthMap.GetLength(0)-yres;
        var maxX = depthMap.GetLength(1)-xres;

        int cx = (maxX + 1) / 2;
        int cy = (maxY + 1) / 2;
        float fx = GetFocal(50f, 36f, 1920f);
        float fy = GetFocal(28.125f, 36*(9/16f), 1080f);

        for (int y = 0; y < maxY; y += yres)
        {
            for (int x = 0; x < maxX; x+=xres)
            {
                // Vertices start in the bottom right, and walk anticlockwise around the quad
                var vertex1 = new Vector3( depthMap[y,x]*((x-cx)/fx), depthMap[y,x]*((y-cy)/fy), depthMap[y,x]);
                var vertex2 = new Vector3( depthMap[y,x+xres]*((x+xres-cx)/fx), depthMap[y,x+xres]*((y-cy)/fy), depthMap[y,x+xres]);
                var vertex3 = new Vector3( depthMap[y+yres,x+xres]*((x+xres-cx)/fx), depthMap[y+yres,x+xres]*((y+yres-cy)/fy), depthMap[y+yres,x+xres] );
                var vertex4 = new Vector3( depthMap[y+yres,x]*((x-cx)/fx), depthMap[y+yres,x]*((y+yres-cy)/fy), depthMap[y+yres,x] );
                
                // var vertex1 = new Vector3( ((x-cx)/fx), ((y-cy)/fy), 1);
                // var vertex2 = new Vector3( ((x+192-cx)/fx), ((y-cy)/fy), 1);
                // var vertex3 = new Vector3( ((x+192-cx)/fx), ((y+108-cy)/fy), 1 );
                // var vertex4 = new Vector3( ((x-cx)/fx), ((y+108-cy)/fy), 1 );

                var triangle1 = new Triangle(vertex1, vertex2, vertex3);
                var triangle2 = new Triangle(vertex1, vertex3, vertex4);

                Triangle[] triangles = [triangle1, triangle2];

                foreach (Triangle triangle in triangles)
                {
                    if (triangle.HasLengthLongerThanThreshold(.1f))
                    {
                        continue;
                    }
                    var normal = triangle.GetNormal();
                    foreach (var position in triangle.GetVerticesAsList())
                    {
                        // UV Coordinate is a simple xy plane projection
                        var texCoord = new Vector2((fy*position[0]/position[2] + cy)/maxY,(fx*position[1]/position[2] + cx)/maxX);
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
    

    public static Mesh nTessellateDepthArray(float[,] depthMap)
    {
        int xres = 4;
        int yres = 4;
        var size = (1080) * (1920);
        
        var uniqueVertices = new HashSet<Vertex>();
        var vertexIndex = new Dictionary<Vertex, int>();

        var positions = new float[size*3];
        var normals = new float[size*3];
        var texCoords = new float[size*2];
        var vertexIndices = new List<int>();

        var posVertexAttribute = new VertexAttribute(0, sizeof(float), 3);
        var normVertexAttribute = new VertexAttribute(3, sizeof(float), 3);
        var texVertexAttribute = new VertexAttribute(6, sizeof(float), 2);
        
        var vertexAttribDictionary = new Dictionary<string, VertexAttribute>();
        vertexAttribDictionary["positions"] = posVertexAttribute;
        vertexAttribDictionary["normals"] = normVertexAttribute;
        vertexAttribDictionary["textureCoordinates"] = texVertexAttribute;

        var currentIndex = 0;

        

        var maxY = depthMap.GetLength(0)-yres;
        var maxX = depthMap.GetLength(1)-xres;

        int cx = (maxX + 1) / 2;
        int cy = (maxY + 1) / 2;
        float fx = GetFocal(50f, 36f, 1920f);
        float fy = GetFocal(28.125f, 36*(9/16f), 1080f);

        for (int y = 0; y < maxY; y += yres)
        {
            for (int x = 0; x < maxX; x+=xres)
            {
                // Vertices start in the bottom right, and walk anticlockwise around the quad
                var vertex1 = new Vector3( depthMap[y,x]*((x-cx)/fx), depthMap[y,x]*((y-cy)/fy), depthMap[y,x]);
                var vertex2 = new Vector3( depthMap[y,x+xres]*((x+xres-cx)/fx), depthMap[y,x+xres]*((y-cy)/fy), depthMap[y,x+xres]);
                var vertex3 = new Vector3( depthMap[y+yres,x+xres]*((x+xres-cx)/fx), depthMap[y+yres,x+xres]*((y+yres-cy)/fy), depthMap[y+yres,x+xres] );
                var vertex4 = new Vector3( depthMap[y+yres,x]*((x-cx)/fx), depthMap[y+yres,x]*((y+yres-cy)/fy), depthMap[y+yres,x] );
                
                var nvertex1 = new Vector3( x, y, 1);
                var nvertex2 = new Vector3( x+xres, y, 1);
                var nvertex3 = new Vector3( x+xres, y+yres, 1 );
                var nvertex4 = new Vector3( x, y+yres, 1 );

                var triangle1 = new Triangle(vertex1, vertex2, vertex3);
                var nonTriangle1 = new Triangle(nvertex1, nvertex2, nvertex3);
                var triangle2 = new Triangle(vertex1, vertex3, vertex4);
                var nonTriangle2 = new Triangle(nvertex1, nvertex3, nvertex4);

                Triangle[] triangles = [triangle1, triangle2];
                Triangle[] nTriangles = [nonTriangle1, nonTriangle2];

                for (int i = 0; i < 2; i++)
                {
                    var triangle = triangles[i];
                    if (triangle.HasLengthLongerThanThreshold(.1f))
                    {
                        continue;
                    }
                    var normal = triangle.GetNormal();
                    var nTriangle = nTriangles[i];
                    for (int j = 0; j < 3; j++)
                    {
                        var position = triangle.GetVerticesAsList()[j];
                        // UV Coordinate is a simple xy plane projection
                        var texCoord = new Vector2((fy*position[0]/position[2] + cy)/maxY,(fx*position[1]/position[2] + cx)/maxX);
                        var vertex = new Vertex(position, normal, texCoord);

                        var nPos = nTriangle.GetVerticesAsList()[j];
                        var idx = (int) (nPos[1] * 1080 + nPos[0]);
                        
                        vertexIndices.Add(idx);
                        positions[idx*3] = position[0];
                        positions[idx*3+1] = position[1];
                        positions[idx*3+2] = position[2];
                        texCoords[idx*2] = texCoord[0];
                        texCoords[idx*2+1] = texCoord[1];
                        normals[idx*3] = normal[0];
                        normals[idx*3+1] = normal[1];
                        normals[idx*3+2] = normal[2];
                    }
                }
            }
        }
        
        var meshLayout = new MeshLayout(8, vertexIndices.ToArray(), vertexAttribDictionary);

        return new Mesh(meshLayout, new List<float>(positions), new List<float>(normals), new List<float>(texCoords));
    }

    public static Mesh TessellateDepthArray(float[,] depthMap)
    {
        
        
        int yres = 4;
        int xres = 4;
        int maxX = depthMap.GetLength(0)-yres;

        int width = depthMap.GetLength(1);
        int height = depthMap.GetLength(0);
        
        int depthBufferTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, depthBufferTexture);
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, depthMap.GetLength(1), depthMap.GetLength(0));
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, depthMap.GetLength(1), depthMap.GetLength(0), PixelFormat.Red, PixelType.Float, depthMap);
        
        var computeShader = new ComputeShader("./ComputeShaders/TessellationComputeShader.glsl");
        
        computeShader.Use();
        GL.BindImageTexture(0, depthBufferTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
        computeShader.SetUniformInt("width", depthMap.GetLength(1));
        computeShader.SetUniformInt("height", depthMap.GetLength(0));

        computeShader.SetUniformInt("xres", xres);
        computeShader.SetUniformInt("yres", yres);
        
        // Generate buffer handles
        int indexBufferHandle = GL.GenBuffer();
        int positionBufferHandle = GL.GenBuffer();
        int texCoordBufferHandle = GL.GenBuffer();
        int normalBufferHandle = GL.GenBuffer();
        
        // Read data from SSBOs
        int[] indexData = new int[width * height * 6];
        float[] positionData = new float[width * height * 3];
        float[] normalData = new float[width * height * 3];
        float[] texCoordData = new float[width * height * 2];
        
        var watch = Stopwatch.StartNew();
        // Bind buffers and allocate storage for them
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, indexBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(int)*indexData.Length, indexData, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, positionBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * positionData.Length, positionData, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, normalBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * normalData.Length, normalData, BufferUsageHint.StaticDraw);
        
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, texCoordBufferHandle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeof(float) * texCoordData.Length, texCoordData, BufferUsageHint.StaticDraw);
        
        // Bind the SSBOs to read their data
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, indexBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, positionBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, texCoordBufferHandle);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, normalBufferHandle);
        watch.Stop();
        Console.WriteLine(watch.ElapsedMilliseconds);

        watch.Reset();
        watch.Start();
        GL.DispatchCompute(depthMap.GetLength(0), depthMap.GetLength(1), 1);
        watch.Stop();
        Console.WriteLine(watch.ElapsedMilliseconds);
        
        // read the data
        GL.GetNamedBufferSubData(indexBufferHandle, 0, sizeof(int) * indexData.Length, indexData);
        GL.GetNamedBufferSubData(positionBufferHandle, 0, sizeof(float) * positionData.Length, positionData);
        GL.GetNamedBufferSubData(texCoordBufferHandle, 0, sizeof(float) * texCoordData.Length, texCoordData);
        GL.GetNamedBufferSubData(normalBufferHandle, 0, sizeof(float) * normalData.Length, normalData);
        
        // Now make a standard mesh out of the data
        
        var posVertexAttribute = new VertexAttribute(0, sizeof(float), 3);
        var normVertexAttribute = new VertexAttribute(3, sizeof(float), 3);
        var texVertexAttribute = new VertexAttribute(6, sizeof(float), 2);
        
        var vertexAttribDictionary = new Dictionary<string, VertexAttribute>();
        vertexAttribDictionary["positions"] = posVertexAttribute;
        vertexAttribDictionary["normals"] = normVertexAttribute;
        vertexAttribDictionary["textureCoordinates"] = texVertexAttribute;
        
        computeShader.Dispose();
        
        var meshLayout = new MeshLayout(8, indexData.ToArray(), vertexAttribDictionary);

        return new Mesh(meshLayout, new List<float>(positionData), new List<float>(normalData), new List<float>(texCoordData));
    }

    public static float GetFocal(float focalLength, float sensorSize, float imgSize)
    {
        return focalLength * (imgSize / sensorSize);
    }

}