namespace RGBDReconstruction.Strategies;

public class FunctionScalarField(Func<float, float, float, float> function) : IScalarField
{
    public float ValueAt(float x, float y, float z)
    {
        return function(x, y, z);
    }
}