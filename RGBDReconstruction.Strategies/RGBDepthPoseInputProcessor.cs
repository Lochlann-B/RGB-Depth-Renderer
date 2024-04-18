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
        //var flatData = new float[height * width];

        int k = 0;
        for (int i = 0; i < data.Length/4; i ++)
        {
            int row = k / width;
            int col = k % width;
            // reshapedDepthValues[row, col] = BitConverter.ToSingle(data.ToArray(), i);
            var floatInBytes = new ReadOnlySpan<byte>([data[i*4], data[i*4 + 1], data[i*4 + 2], data[i*4 + 3]]);
            var floatVal = BitConverter.ToSingle(floatInBytes);
            reshapedDepthValues[row, col] = floatVal;
            k++;
        }
        
        // var flatDepthData = new List<float>();
        //
        // for (int i = 0; i < data.Length; i += 4)
        // {
        //     watch.Start();
        //     var floatInBytes = new ReadOnlySpan<byte>([data[i], data[i + 1], data[i + 2], data[i + 3]]);
        //     var floatVal = BitConverter.ToSingle(floatInBytes);
        //     flatDepthData.Add(floatVal);
        //     watch.Stop();
        //     Console.WriteLine(watch.ElapsedMilliseconds);
        //     watch.Reset();
        // }
        
        //var reshapedDepthValues = new float[height, width];
        //
       // Buffer.BlockCopy(flatData, 0, reshapedDepthValues, 0, flatData.Length * sizeof(float));

        return reshapedDepthValues;
    }
}