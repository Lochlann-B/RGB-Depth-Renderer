namespace RGBDReconstruction.Application;

public class MultiViewFramePreparer
{
    private MultiViewProcessor _viewProcessor;

    private List<Texture> _rgbTextures;
    private List<Texture> _depthTextures;

    private double _elapsedTimeSinceLastFrame = 0d;

    public void Init()
    {
        
    }

    public void TryUpdateNextFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return;
        }

        _elapsedTimeSinceLastFrame = 0d;

        var nextFrameData = _viewProcessor.GetNextAvailableFrame();
        if (nextFrameData is null)
        {
            return;
        }
        
        
    }
}