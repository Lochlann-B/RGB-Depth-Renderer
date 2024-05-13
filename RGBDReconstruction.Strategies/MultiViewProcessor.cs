using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTK.Mathematics;
using RGBDReconstruction.Strategies;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace RGBDReconstruction.Application;

public class MultiViewProcessor(String directoryPath)
{
    protected int _numCams = 0;
    
    public String DirectoryPath { get; set; } = directoryPath;

    private ConcurrentPriorityQueue<int, byte[]>[] _RGBFrameDatas;
    private ConcurrentPriorityQueue<int, float[,]>[] _depthFrameDatas;

    private SemaphoreSlim _semaphoreRGB = new(12);
    private SemaphoreSlim _semaphoreDepth = new(12);

    protected int _maxFrameDataSize = 1;


    protected int _currentFrameRGB = 1;
    protected int _currentFrameDepth = 1;
    
    protected int _nextFrameToReturn = 1;

    protected bool _notReachedFinalRGBFrame = true;
    protected bool _notReachedFinalDepthFrame = true;

    public List<(byte[], float[,])> GetFirstFrame()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        var result = new List<(byte[], float[,])>();
        for (int i = 1; i <= _numCams; i++)
        {
            using var img = File.OpenRead(GetPNGFileName(1, i));
            var rgb = ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha).Data;
            var depth = GetDepthMap(1, i);
            result.Add((rgb, depth));
        }

        return result;
    }

    public (byte[], float[,])[]? GetNextAvailableFrame()
    {

        if (!NextFrameDataIsPreparedForAllCameras())
        {
            return null;
        }

        _nextFrameToReturn++;
        var frameData = new (byte[], float[,])[_numCams];
        for (int i = 0; i < _RGBFrameDatas.Length; i++)
        {
            _RGBFrameDatas[i].TryDequeue(out var rgbData);
            _depthFrameDatas[i].TryDequeue(out var depthData);
            frameData[i] = (rgbData.Value, depthData.Value);
        }

        return frameData;
    }

    private bool NextFrameDataIsPreparedForAllCameras()
    {
        var isReady = true;
        
        for (int i = 0; i < _RGBFrameDatas.Length; i++)
        {
            var _RGBFrameData = _RGBFrameDatas[i];
            var _depthFrameData = _depthFrameDatas[i];
            
            isReady = isReady && !((_RGBFrameDatas[i].Count < 20 || _depthFrameDatas[i].Count < 20) &&
                                   _nextFrameToReturn == 1);

            isReady = isReady && !(!_RGBFrameDatas[i].TryPeek(out var _) || !_depthFrameDatas[i].TryPeek(out var _));
        
            _RGBFrameData.TryPeek(out var rgb);
            isReady = isReady && rgb.Key == _nextFrameToReturn;

            _depthFrameData.TryPeek(out var depth);
            isReady = isReady && (depth.Key == _nextFrameToReturn);
        }

        return isReady;
    }

    public void LoadFramesDepthAllCams()
    {
        Task.Run((LoadFramesDepthAsync));
    }
    
    public void LoadFramesRGBAllCams()
    {
        Task.Run((LoadFramesRGBAsync));
    }

    public async Task LoadFramesDepthAsync()
    {
        try
        {
            while (_notReachedFinalDepthFrame)
            {
                
                
                if (_depthFrameDatas[0].Count > _maxFrameDataSize)
                {
     
                    await Task.Delay(5);
                    continue;
                }
                await _semaphoreDepth.WaitAsync();
                
                int frameIdx = Interlocked.Increment(ref _currentFrameDepth) - 1;
                Task.Run(() => LoadFrameForEachDepthCam(frameIdx)).ContinueWith(t => _semaphoreDepth.Release(),
                    TaskContinuationOptions.OnlyOnRanToCompletion);

            }
        } catch(Exception e)
        {
            Console.WriteLine("Error or all depth frames loaded: " + e);
        } 
    }

    public void LoadFrameForEachDepthCam(int frameIdx)
    {
        try
        {
            for (int i = 0; i < _numCams; i++)
            {
                int camIdx = i;
                
                LoadFrameDepthAsync(frameIdx, GetDepthMapFileName(frameIdx, camIdx), camIdx);

            }
        }
        catch (Exception e)
        {

            _notReachedFinalDepthFrame = false;
        }
    }
    
    public void LoadFrameForEachRGBCam(int frameIdx)
    {
        try
        {
            for (int i = 0; i < _numCams; i++)
            {
                int camIdx = i;

                LoadFrameRGBAsync(frameIdx, GetPNGFileName(frameIdx, camIdx + 1), camIdx);
              
            }
        }
        catch (Exception e)
        {
            _notReachedFinalRGBFrame = false;
        }
    }
    
    public async Task LoadFramesRGBAsync()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        try
        {
            while (_notReachedFinalRGBFrame)
            {
                if (_RGBFrameDatas[0].Count > _maxFrameDataSize)// || _semaphoreRGB.CurrentCount == 0)
                {
                    // _semaphoreRGB.Release();
                    await Task.Delay(5);
                    continue;
                }
                await _semaphoreRGB.WaitAsync();
                
                int frameIdx = Interlocked.Increment(ref _currentFrameRGB) - 1;

                Task.Run((() => LoadFrameForEachRGBCam(frameIdx))).ContinueWith(t => _semaphoreRGB.Release(),
                    TaskContinuationOptions.OnlyOnRanToCompletion);
            }
        } catch(Exception e)
        {
            Console.WriteLine("Error or all RGB frames loaded: " + e);
        }
    }

    private async Task LoadFrameRGBAsync(int frameNo, string imgPath, int cam)
    {
        //await Task.Run(() =>
       // {
       try
       {
           var idx = cam;
           using var img = File.OpenRead(imgPath);
           _RGBFrameDatas[idx].Enqueue(frameNo, ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha).Data);
       }
       catch (Exception e)
       {
           //Console.WriteLine(e);
           _notReachedFinalRGBFrame = false;
       }
       //});
    }

    public byte[] GetRGBImageData(int frame, int cam)
    {
        var fileStr = GetPNGFileName(frame, cam);

        using var img = File.OpenRead(fileStr);
        return ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha).Data;
    }

    public List<Byte[]> GetRGBImageDataAllCams(int frame)
    {
        var res = new List<Byte[]>();

        for (int i = 0; i < _numCams; i++)
        {
            var idx = i + 1;

            var fileStr = GetPNGFileName(frame, idx);

            using var img = File.OpenRead(fileStr);
            res.Add(ImageResult.FromStream(img, ColorComponents.RedGreenBlueAlpha).Data);
        }

        return res;
    }

    private async Task LoadFrameDepthAsync(int frameNo, string imgPath, int cam)
    {
        // uses camera 1
        try
        {
            int idx = cam;
            var depthMap = GetDepthMap(frameNo, idx + 1);
            _depthFrameDatas[idx].Enqueue(frameNo, depthMap);
        }
        catch (Exception e)
        {
            //Console.WriteLine(e);
            _notReachedFinalDepthFrame = false;
        }
        
    }

    public List<Matrix4> GetCameraPoseInformation(bool isVoxel)
    {
        // int numesleft = 2;
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
                        // numesleft--;
                        
                        

                        if (isVoxel)
                        {
                            var Rx = Matrix4.CreateRotationX(-(float)Math.PI*((-rx)+90f)/180f);
                            
                        
                            var Ry = Matrix4.CreateRotationY((float)Math.PI*(rz+180f)/180f);
                            var Rz = Matrix4.CreateRotationZ((float)Math.PI*ry/180f);
                            var T = Matrix4.CreateTranslation(new Vector3(px, -pz, py));
                            // var R = Rx * Ry * Rz * T;
                            var R = Rz * Ry * Rx * T;
                            //var R = T;
                            //R[2,2] *= -1;
                            list.Add(R);
                        }
                        else
                        {
                            var Rx = Matrix4.CreateRotationX((float)Math.PI*(rx-90f)/180f);
                            var Ry = Matrix4.CreateRotationY((float)Math.PI*(rz)/180f);
                            var Rz = Matrix4.CreateRotationZ((float)Math.PI*(-ry)/180f);
                            var T = Matrix4.CreateTranslation(new Vector3(px, pz, py));
                            var rot = Rz * Ry * Rx;
                            var R = rot.Inverted() * T;
                            //var R = T;
                            //R[2,2] *= -1;
                            list.Add(R.Inverted());
                        }
                        
                        
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
            
            _numCams = list.Count;
            _RGBFrameDatas = new ConcurrentPriorityQueue<int, byte[]>[_numCams];
            _depthFrameDatas = new ConcurrentPriorityQueue<int, float[,]>[_numCams];
            for (int i = 0; i < _numCams; i++)
            {
                _RGBFrameDatas[i] = new ConcurrentPriorityQueue<int, byte[]>();
                _depthFrameDatas[i] = new ConcurrentPriorityQueue<int, float[,]>();
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
        var fileName = GetDepthMapFileName(frame, camera);
        return RGBDepthPoseInputProcessor.GetCameraLocalDepthMapFromExrFile(fileName);
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