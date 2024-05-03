using System.Collections.Concurrent;
using FFmpeg.AutoGen;
using OpenTK.Mathematics;
using RGBDReconstruction.Application;
using VideoHandler;

namespace RGBDReconstruction.Strategies;

public class MultiViewVideoProcessor : MultiViewProcessor
{
    private List<VideoStreamHandler> _videoStreamHandlersDepth = [];
    private List<VideoStreamHandler> _videoStreamHandlersRGB = [];

    private ConcurrentPriorityQueue<int, IntPtr>[] _videoFramesDepth;
    private ConcurrentPriorityQueue<int, IntPtr>[] _videoFramesRGB;

    public MultiViewVideoProcessor(string directoryPath) : base(directoryPath)
    { }

    public void PrepareVideoFiles()
    {
        VideoStreamHandler.Init();
        
        _videoFramesDepth = new ConcurrentPriorityQueue<int, IntPtr>[_numCams];
        _videoFramesRGB = new ConcurrentPriorityQueue<int, IntPtr>[_numCams];
        
        for (int i = 0; i < _numCams; i++)
        {
            _videoFramesRGB[i] = new ConcurrentPriorityQueue<int, IntPtr>();
            _videoFramesDepth[i] = new ConcurrentPriorityQueue<int, IntPtr>();
        }
        // _RGB
        
        for (int i = 0; i < _numCams; i++)
        {
            var fileNameRGB = GetVideoRGBFileName(i + 1, ".mkv", "");
            var fileNameDepth = GetVideoDepthFileName(i + 1, ".mkv", "");
            var idx = i;
            Task.Run((() =>
            {
                var videoStreamHandler = new VideoStreamHandler();
                videoStreamHandler.Initialize(fileNameRGB);
                _videoStreamHandlersRGB.Add(videoStreamHandler);
                // LoadFramesDepthAsync();
                LoadFramesFromVideoStream(videoStreamHandler, idx, _videoFramesRGB);
                
                
            }));

            Task.Run((() =>
            {
                
                var videoStreamHandler = new VideoStreamHandler();
                videoStreamHandler.Initialize(fileNameDepth);
                _videoStreamHandlersDepth.Add(videoStreamHandler);
                LoadFramesFromVideoStream(videoStreamHandler, idx, _videoFramesDepth);
                // LoadFramesDepthAsync();
                // LoadFramesRGBAsync();
            }));

        }
    }

    public async unsafe Task<(IntPtr,IntPtr)[]> Begin()
    {
        var res = new (IntPtr, IntPtr)[_numCams];
        for (int i = 0; i < _numCams; i++)
        {
            var dataRGB = new IntPtr(_videoStreamHandlersRGB[i].GetNextFrame()->data[0]);
            var dataDepth =  new IntPtr(_videoStreamHandlersDepth[i].GetNextFrame()->data[0]);
            // TODO: Add depth to 2nd argument
            res[i] = (dataRGB, dataDepth);
        }
        
        return res;
    }

    public (IntPtr, IntPtr)[]? AwaitNextFrame()
    {
        while (true)
        {
            var res = GetNextAvailableVideoFrame();
            if (res is null)
            {
                continue;
            }

            return res;
        }
    }

    public (IntPtr, IntPtr)[]? GetNextAvailableVideoFrame()
    {
        if (!NextFrameDataPreparedForAllCameras())
        {
            return null;
        }

        var frameData = new (IntPtr, IntPtr)[_numCams];
        for (int i = 0; i < frameData.Length; i++)
        {
            _videoFramesRGB[i].TryDequeue(out var rgbData);
            _videoFramesDepth[i].TryDequeue(out var depthData);

            if (rgbData.Value == IntPtr.Zero)
            {
                var egg = true;
            }
            
            frameData[i] = (rgbData.Value, depthData.Value);
        }

        return frameData;
    }
    

    private bool NextFrameDataPreparedForAllCameras()
    {
        var isReady = true;

        for (int i = 0; i < _numCams; i++)
        {
            var _RGBFrameData = _videoFramesRGB[i];
            var _depthFrameData = _videoFramesDepth[i];
            
            // isReady = isReady && !((_videoFramesRGB[i].Count < 20 || _videoFramesDepth[i].Count < 20) &&
            //                        _nextFrameToReturn == 1);

            isReady = isReady && !(_videoFramesDepth[i].IsEmpty) && !_videoFramesRGB[i].IsEmpty;

            var rgbPeek = _videoFramesRGB[i].TryPeek(out var rgb);

            var depthPeek = _videoFramesDepth[i].TryPeek(out var depth);
            
            isReady = isReady && !(!rgbPeek || !depthPeek);

            isReady = isReady && rgb.Value != 0 && depth.Value != 0;

            // _RGBFrameData.TryPeek(out var rgb);
            // isReady = isReady && rgb.Key == _nextFrameToReturn;
            //
            // _depthFrameData.TryPeek(out var depth);
            // isReady = isReady && (depth.Key == _nextFrameToReturn);

        }

        return isReady;
    }

    private unsafe void LoadFramesFromVideoStream(VideoStreamHandler vidStreamHandler, int idx, ConcurrentPriorityQueue<int, IntPtr>[] queues)
    {
        var hasNextFrame = true;
        try
        {
            while (hasNextFrame)
            {
                if (queues[idx].Count > _maxFrameDataSize)
                {
                    continue;
                }
                
                int frameIdx = Interlocked.Increment(ref _currentFrameDepth) - 1;

             
                var data = new IntPtr(vidStreamHandler.GetNextFrame()->data[0]);
                if (data == IntPtr.Zero)
                {
                    hasNextFrame = false;
                    break;
                }
                queues[idx].Enqueue(frameIdx,data);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("All depth video frames loaded or error: " + e);
        }
    }

    public new unsafe void LoadFramesDepthAsync()
    {
        var hasNextFrame = true;
        try
        {
            while (hasNextFrame)
            {
                if (_videoFramesDepth.Length > _maxFrameDataSize)
                {
                    continue;
                }
                
                int frameIdx = Interlocked.Increment(ref _currentFrameDepth) - 1;
                for (int i = 0; i < _numCams; i++)
                {
                    // TODO :Add depth
                    var data = new IntPtr(_videoStreamHandlersRGB[i].GetNextFrame()->data[0]);
                    if (data == IntPtr.Zero)
                    {
                        hasNextFrame = false;
                        break;
                    }
                    _videoFramesDepth[i].Enqueue(frameIdx,data);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("All depth video frames loaded or error: " + e);
        }
    }
    
    public new void LoadFramesDepthAllCams()
    {
        Task.Run((LoadFramesDepthAsync));
    }
    
    public new void LoadFramesRGBAllCams()
    {
        Task.Run((LoadFramesRGBAsync));
    }

    public new unsafe void LoadFramesRGBAsync()
    {
        var hasNextFrame = true;
        try
        {
            while (hasNextFrame)
            {
                if (_videoFramesRGB.Length > _maxFrameDataSize)
                {
                    continue;
                }
                
                int frameIdx = Interlocked.Increment(ref _currentFrameRGB) - 1;
                for (int i = 0; i < _numCams; i++)
                {
                    var data = new IntPtr(_videoStreamHandlersRGB[i].GetNextFrame()->data[0]);
                    if (data == IntPtr.Zero)
                    {
                        hasNextFrame = false;
                        break;
                    }
                    _videoFramesRGB[i].Enqueue(frameIdx,data);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("All depth video frames loaded or error: " + e);
        }
    }
    
    private String GetVideoRGBFileName(int cam, String extension, String path)
    {
        var numCamDigits = (int)Math.Floor(Math.Log10(cam)) + 1;
        
        // max is 4 digits
        var camStr = "";
        
        for (int c = 0; c < (3 - numCamDigits); c++)
        {
            camStr += "0";
        }

        camStr += cam.ToString();

        // if (cam == 1)
        // {
            return DirectoryPath + "\\cam_" + camStr + "\\huffyuv_slowest_rgb_cam_" + camStr + extension;
        // }
        // else
        // {
            // return DirectoryPath + "\\cam_" + camStr + "\\rgb_cam_" + camStr + extension;
        // }
    }
    
    private String GetVideoDepthFileName(int cam, String extension, String path)
    {
        var numCamDigits = (int)Math.Floor(Math.Log10(cam)) + 1;
        
        // max is 4 digits
        var camStr = "";
        
        for (int c = 0; c < (3 - numCamDigits); c++)
        {
            camStr += "0";
        }

        camStr += cam.ToString();

        return DirectoryPath + "\\cam_" + camStr + "\\huffyuv_slowest_depth_cam_" + camStr + extension;
    }
}