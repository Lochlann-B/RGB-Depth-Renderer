using System.Net;

namespace RGBDReconstruction.Application;

using System.Linq;

public class Mesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> texCoords)
{

    public float[] GetContiguousMeshData()
    {
        var vertexAttribs = MeshLayout.VertexAttributes;
        var posVertexAttribs = vertexAttribs["positions"];
        var normVertexAttribs = vertexAttribs["normals"];
        var texVertexAttribs = vertexAttribs["textureCoordinates"];

        int numPosComponents = posVertexAttribs.NumComponents;
        int numNormComponents = normVertexAttribs.NumComponents;
        
        int stride = MeshLayout.Stride;

        float[] contiguousMeshData = new float[VertexNormals.Count + VertexPositions.Count + texCoords.Count];

        int idx = 1;
        int currentUpperLimitIndex = numPosComponents;
        VertexPositions.ForEach(pos =>
        {
            contiguousMeshData[idx-1] = pos;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numPosComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        idx = 1 + numPosComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents;
        VertexNormals.ForEach(norm =>
        {
            contiguousMeshData[idx-1] = norm;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numNormComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        int numTexComponents = texVertexAttribs.NumComponents;
        idx = 1 + numNormComponents + numPosComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents + numTexComponents;
        texCoords.ForEach(tex =>
        {
            contiguousMeshData[idx-1] = tex;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numTexComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });

        return contiguousMeshData;
    }

    public MeshLayout MeshLayout { get; } = meshLayout;
    public List<float> VertexPositions { get; } = vertexPositions;
    public List<float> VertexNormals { get; } = vertexNormals;
}