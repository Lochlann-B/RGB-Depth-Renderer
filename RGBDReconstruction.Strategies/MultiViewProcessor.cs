using System.Diagnostics;
using OpenTK.Mathematics;
using RGBDReconstruction.Strategies;
using System.IO;

namespace RGBDReconstruction.Application;

public class MultiViewProcessor(String directoryPath)
{
    public String DirectoryPath { get; set; } = directoryPath;

    public List<Matrix4> GetCameraPoseInformation()
    {
        string filePath = directoryPath + "/camera_poses.txt";
        var list = new List<Matrix4>();
        try
        {
            var lines = File.ReadAllLines(filePath);
            float px = 0f;
            float py = 0f;
            float pz = 0f;
            float rx = 0f;
            float ry = 0f;
            float rz = 0f;
            
            // Read each line in the file
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (i == 0)
                {
                    continue;
                }
                
                
                string[] parts = line.Split(' ');

                if (parts.Length == 0)
                {
                    continue;
                }

                var part = parts[0];
                
                switch (part)
                {
                    case "e":
                        var Rx = Matrix4.CreateRotationX((float)Math.PI*(rx-90f)/180f);
                        var Ry = Matrix4.CreateRotationY((float)Math.PI*(rz)/180f);
                        var Rz = Matrix4.CreateRotationZ((float)Math.PI*ry/180f);
                        var T = Matrix4.CreateTranslation(new Vector3(px, pz, py));
                        var R = Rz * Ry * Rx * T;
                        //R[2,2] *= -1;
                        list.Add(R); //R.Inverted());
                        //list.Add(Matrix4.Identity);
                        break;
                    case "px":
                        if (float.TryParse(parts[1], out float valpx))
                        {
                            px = valpx;
                        }

                        break;
                    case "py":
                        if (float.TryParse(parts[1], out float valpy))
                        {
                            py = valpy;
                        }

                        break;
                    case "pz":
                        if (float.TryParse(parts[1], out float valpz))
                        {
                            pz = valpz;
                        }

                        break;
                    case "rx":
                        if (float.TryParse(parts[1], out float valrx))
                        {
                            rx = valrx;
                        }

                        break;
                    case "ry":
                        if (float.TryParse(parts[1], out float valry))
                        {
                            ry = valry;
                        }

                        break;
                    case "rz":
                        if (float.TryParse(parts[1], out float valrz))
                        {
                            rz = valrz;
                        }

                        break;
                }
                
            }

            return list;
        }
        catch (IOException e)
        {
            Console.WriteLine($"An error occurred while reading the file: {e.Message}");
            throw;
        }
    }

    public float[,] GetDepthMap(int frame, int camera)
    {
        return RGBDepthPoseInputProcessor.GetCameraLocalDepthMapFromExrFile(GetDepthMapFileName(frame, camera));
    }
    
    private String GetDepthMapFileName(int frame, int cam)
    {
        var numFrameDigits = (int)Math.Floor(Math.Log10(frame)) + 1;
        var numCamDigits = (int)Math.Floor(Math.Log10(cam)) + 1;
        
        // max is 4 digits
        var frameStr = "";
        var camStr = "";
        for (int f = 0; f < (4 - numFrameDigits); f++)
        {
            frameStr += "0";
        }

        frameStr += frame.ToString();
        
        for (int c = 0; c < (3 - numCamDigits); c++)
        {
            camStr += "0";
        }

        camStr += cam.ToString();

        return DirectoryPath + @"\depth\" + "frame_" + frameStr + "_cam_" + camStr + ".exr";
    }
}