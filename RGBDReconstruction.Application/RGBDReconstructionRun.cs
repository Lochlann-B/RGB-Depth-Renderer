using OpenTK.Windowing.Common;

namespace RGBDReconstruction.Application;

// OpenGL and OpenTK boilerplate obtained and adapted from https://opentk.net/learn under CC (Creative Commons) 4.0 open license.

public class RGBDReconstructionRun
{
    static void Main(String[] args)
    {
        using var app = new Application(800, 600, "RGB+Depth Reconstruction");
        app.Run();
    }
}
