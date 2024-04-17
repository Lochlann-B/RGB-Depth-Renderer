using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTK.Mathematics;
using RGBDReconstruction.Strategies;
using System.IO;
using StbImageSharp;

namespace RGBDReconstruction.Application;

public class MultiViewProcessor(String directoryPath)
{
    public String DirectoryPath { get; set; } = directoryPath;

    private ConcurrentPriorityQueue<int, byte[]> _RGBFrameData = new();
    private ConcurrentPriorityQueue<int, float[,]> _depthFrameData = new();

    private SemaphoreSlim _semaphoreRGB = new(6);
    private SemaphoreSlim _semaphoreDepth = new(6);

    private int _maxFrameDataSize = 600;

    private int _currentFrameRGB = 1;
    private int _currentFrameDepth = 1;
    private int _nextFrameToReturn = 1;

    public (byte[], float[,])? GetNextAvailableFrame()
    {
        if ((_RGBFrameData.Count < 200 || _depthFrameData.Count < 200) && _nextFrameToReturn == 1)
        {
            return null;
        }

        if (!_RGBFrameData.TryPeek(out var _) || !_depthFrameData.TryPeek(out var _)) return null;

        _RGBFrameData.TryPeek(out var rgb);
        if (rgb.Key != _nextFrameToReturn) return null;

        _depthFrameData.TryPeek(out var depth);
        if (depth.Key != _nextFrameToReturn) return null;

        _nextFrameToReturn++;
        _RGBFrameData.TryDequeue(out var rgbData);
        _depthFrameData.TryDequeue(out var depthData);
        return (rgbData.Value, depthData.Value);
    }

    public async Task LoadFramesDepthAsync()
    {
        try
        {
            while (true)
            {
                if (_depthFrameData.Count > _maxFrameDataSize || _semaphoreDepth.CurrentCount == 0)
                {
                    //await Task.Delay(5);
                    continue;
                }

                await _semaphoreDepth.WaitAsync();
                
                // uses camera 1
                int frameIdx = Interlocked.Increment(ref _currentFrameDepth) - 1;
                Task.Run(() => LoadFrameDepthAsync(frameIdx, GetDepthMapFileName(frameIdx, 1))).ContinueWith(t => _semaphoreDepth.Release(), TaskContinuationOptions.OnlyOnRanToCompletion);
                //_currentFrameDepth++;
            }
        } catch(Exception e)
        {
            Console.WriteLine("Error or all depth frames loaded: " + e);
        } 
    }
    
    public async Task LoadFramesRGBAsync()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        try
        {
            while (true)
            {
                if (_RGBFrameData.Count > _maxFrameDataSize || _semaphoreRGB.CurrentCount == 0)
                {
                    //await Task.Delay(5);
                    continue;
                }

                await _semaphoreRGB.WaitAsync();
                
                // uses camera 1
                int frameIdx = Interlocked.Increment(ref _currentFrameRGB) - 1;
                Task.Run(() => LoadFrameRGBAsync(frameIdx, GetPNGFileName(frameIdx, 1))).ContinueWith(t => _semaphoreRGB.Release(), TaskContinuationOptions.OnlyOnRanToCompletion);
                //_currentFrameRGB++;
            }
        } catch(Exception e)
        {
            Console.WriteLine("Error or all RGB frames loaded: " + e);
        }
    }

    private async Task LoadFrameRGBAsync(int frameNo, string imgPath)
    {
        await Task.Run(() =>
        {
            using var img = File.OpenRead(imgPath);
            _RGBFrameData.Enqueue(frameNo, ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha).Data);
        });
    }

    private async Task LoadFrameDepthAsync(int frameNo, string imgPath)
    {
        await Task.Run(() =>
        {
            // uses camera 1
            _depthFrameData.Enqueue(frameNo, GetDepthMap(frameNo, 1));
        });
    }

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
    
    public String GetDepthMapFileName(int frame, int cam)
    {
        return GetFileName(frame, cam, ".exr", @"\depth\");
    }

    public String GetPNGFileName(int frame, int cam)
    {
        return GetFileName(frame, cam, ".png", @"\rgb\");
    }

    private String GetFileName(int frame, int cam, String extension, String path)
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

        return DirectoryPath + path + "frame_" + frameStr + "_cam_" + camStr + extension;
    }
}