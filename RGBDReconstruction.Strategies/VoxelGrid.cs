using System.Numerics;

namespace RGBDReconstruction.Strategies;

public class VoxelGrid(int size, float xStart, float yStart, float zStart, float resolution) : IVoxelGrid
{
    private float[] _voxelValues = new float[size * size * size];

    public float this[float x, float y, float z]
    {
        get => _voxelValues[Index(x, y, z)];
        set => _voxelValues[Index(x, y, z)] = value;
    }

    private int Index(float x, float y, float z)
    {
        var nX = (x - XStart).RoundToInterval(Resolution);
        var nY = (y - YStart).RoundToInterval(Resolution);
        var nZ = (z - ZStart).RoundToInterval(Resolution);

        return (int)(nX / Resolution) + (int)((nY / Resolution) * Size) + (int)((nZ / Resolution) * Size * Size);
    }

    public float Resolution { get; } = resolution;
    public int Size { get; } = size;
    public float XStart { get; } = xStart;
    public float YStart { get; } = yStart;
    public float ZStart { get; } = zStart;
}