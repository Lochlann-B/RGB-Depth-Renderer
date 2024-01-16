namespace RGBDReconstruction.Strategies;

public interface IScalarField
{
    float ValueAt(float x, float y, float z);
}