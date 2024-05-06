namespace Geometry;

public class ColouredMesh : Mesh
{
    public ColouredMesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> colours) : base(meshLayout, vertexPositions, vertexNormals, colours)
    {
        this.VertexPositions = vertexPositions;
        this.MeshLayout = meshLayout;
        this.VertexNormals = vertexNormals;
        this.Colours = colours;
    }

    public new float[] GetContiguousMeshData()
    {
        var vertexAttribs = MeshLayout.VertexAttributes;
        var posVertexAttribs = vertexAttribs["positions"];
        var normVertexAttribs = vertexAttribs["normals"];
        var colourVertexAttribs = vertexAttribs["colours"];

        int numPosComponents = posVertexAttribs.NumComponents;
        int numNormComponents = normVertexAttribs.NumComponents;
        
        int stride = MeshLayout.Stride;

        float[] contiguousMeshData = new float[VertexNormals.Count + VertexPositions.Count + Colours.Count];

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
        int numColComponents = colourVertexAttribs.NumComponents;
        idx = 1 + numNormComponents + numPosComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents + numColComponents;
        Colours.ForEach(col =>
        {
            contiguousMeshData[idx-1] = col;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numColComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        
        return contiguousMeshData;
    }

    public List<float> Colours { get; set; }
    public MeshLayout MeshLayout { get; }
    public List<float> VertexPositions { get; }
    public List<float> VertexNormals { get; }
}