namespace RGBDReconstruction.Application;

public class VertexAttribute(int offset, int size, int numComponents)
{
    public int NumComponents { get; set; } = numComponents;

    public int Offset { get; set; } = offset;

    public int Size { get; set; } = size;
}