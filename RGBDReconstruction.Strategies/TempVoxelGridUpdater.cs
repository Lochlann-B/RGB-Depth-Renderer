namespace RGBDReconstruction.Strategies;

public class TempVoxelGridUpdater
{
    public static IVoxelGrid getExampleVoxelGrid()
    {
        var scalarField = new FunctionScalarField((float x, float y, float z) => z-1.0f);

        var voxelGrid = new VoxelGrid(50, -0.5f, -0.5f, 0.5f, 0.02f);
        for (float i = -0.5f; i < 0.5; i+=0.02f)
        {
            for (float j = -0.5f; j < 0.5f; j+=0.02f)
            {
                for (float k = 0.5f; k < 1.49f; k+=0.02f)
                {
                    
                    voxelGrid[i, j, k] = scalarField.ValueAt(i, j, k);
                    
                }
            }
        }

        return voxelGrid;
    }
}