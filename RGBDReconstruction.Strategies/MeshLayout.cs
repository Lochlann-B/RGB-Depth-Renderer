namespace RGBDReconstruction.Application;

public class MeshLayout(int stride, int[] indexArray, Dictionary<String, VertexAttribute> vertexAttributes)
{
    public int Stride { get; set; } = stride;
    private int[] IndexElementArray { get; } = indexArray;

public Dictionary<String, VertexAttribute> VertexAttributes
    {
        get => vertexAttributes;
        set => vertexAttributes = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int[] IndexArray { get; } = indexArray;
}