namespace RGBDReconstruction.Strategies;

public class TempVoxelGridUpdater
{
    public static IVoxelGrid getExampleVoxelGrid()
    {
        var scalarField = new FunctionScalarField((float x, float y, float z) => x * x + y * y + z * z - 25);

        var voxelGrid = new VoxelGrid(101, -5, -5, -5, 0.1f);
        for (float i = -5; i < 5; i+=0.1f)
        {
            for (float j = -5; j < 5; j+=0.1f)
            {
                for (float k = -5; k < 5; k+=0.1f)
                {
                    if (i + j * 101 + k * 101 * 101 < 101 * 101 * 101)
                    {
                        voxelGrid[i, j, k] = scalarField.ValueAt(i, j, k);
                    }
                }
            }
        }

        return voxelGrid;
    }
}