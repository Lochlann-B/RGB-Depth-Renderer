using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using TinyEXR;
using TinyEXR.Native;

namespace RGBDReconstruction.Strategies;


public class RGBDepthPoseInputProcessor
{
    public static float[,] GetCameraLocalDepthMapFromExrFile(string path)
    {
        var image = new SinglePartExrReader();
        image.Read(path);

        var data = image.GetImageData(0);
        int width = image.Width;
        int height = image.Height;

        var reshapedDepthValues = new float[height, width];

        int k = 0;
        for (int i = 0; i < data.Length/4; i ++)
        {
            int row = k / width;
            int col = k % width;

            var floatInBytes = new ReadOnlySpan<byte>([data[i*4], data[i*4 + 1], data[i*4 + 2], data[i*4 + 3]]);
            var floatVal = BitConverter.ToSingle(floatInBytes);
            reshapedDepthValues[row, col] = floatVal;
            k++;
        }

        return reshapedDepthValues;
    }
}