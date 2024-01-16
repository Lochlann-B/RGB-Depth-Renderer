namespace RGBDReconstruction.Strategies;

public interface IVoxelGrid
{
    public float this[float x, float y, float z] { get; set; }
    
    public float Resolution { get; }
    public int Size { get; }
    public float XStart { get; }
    public float YStart { get; }
    public float ZStart { get; }
}