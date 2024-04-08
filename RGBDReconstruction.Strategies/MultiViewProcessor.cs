namespace RGBDReconstruction.Application;

public class MultiViewProcessor(String directoryPath)
{
    protected int currentFrame = 0;
    
    
    // function that gets next frame
    // : get next depth map and next rgb frame
    //   for each camera


    public String DirectoryPath { get; set; } = directoryPath;
}