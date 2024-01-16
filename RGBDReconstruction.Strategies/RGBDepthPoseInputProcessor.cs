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

        var flatDepthData = new List<float>();
        
        for (int i = 0; i < data.Length; i += 4)
        {
            var floatInBytes = new ReadOnlySpan<byte>([data[i], data[i + 1], data[i + 2], data[i + 3]]);
            var floatVal = BitConverter.ToSingle(floatInBytes);
            flatDepthData.Add(floatVal);
        }

        var reshapedDepthValues = new float[height, width];
        
        Buffer.BlockCopy(flatDepthData.ToArray(), 0, reshapedDepthValues, 0, flatDepthData.Count * sizeof(float));

        return reshapedDepthValues;
    }
}