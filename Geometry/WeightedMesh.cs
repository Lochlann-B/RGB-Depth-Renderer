namespace Geometry;

public class WeightedMesh : Mesh
{
    public WeightedMesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> texCoords, List<float> weights, List<float> xRanges, List<float> yRanges, List<float> zRanges) : base(meshLayout, vertexPositions, vertexNormals, texCoords, xRanges, yRanges, zRanges)
    {
        this.Weights = weights;
        this.TexCoords = texCoords;
    }

    public WeightedMesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> texCoords, List<float> weights) : base(meshLayout, vertexPositions, vertexNormals, texCoords)
    {
        this.Weights = weights;
        this.TexCoords = texCoords;
    }
    
    public float[] GetContiguousMeshData()
    {
        var vertexAttribs = MeshLayout.VertexAttributes;
        var posVertexAttribs = vertexAttribs["positions"];
        var normVertexAttribs = vertexAttribs["normals"];
        var texVertexAttribs = vertexAttribs["textureCoordinates"];
        var weightVertexAttribs = vertexAttribs["weights"];

        int numPosComponents = posVertexAttribs.NumComponents;
        int numNormComponents = normVertexAttribs.NumComponents;
        
        int stride = MeshLayout.Stride;

        float[] contiguousMeshData = new float[VertexNormals.Count + VertexPositions.Count + TexCoords.Count + Weights.Count];

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
        TexCoords.ForEach(tex =>
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


        int numWeightComponents = weightVertexAttribs.NumComponents;
        idx = 1 + numNormComponents + numPosComponents + numTexComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents + numTexComponents + numWeightComponents;
        Weights.ForEach(weight =>
        {
            contiguousMeshData[idx - 1] = weight;
            if ((idx) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numWeightComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        
        return contiguousMeshData;
    }

    public List<float> Weights { get; set; }
    public List<float> TexCoords { get; set; }
}