using System.Collections.Concurrent;
using FFmpeg.AutoGen;
using RGBDReconstruction.Application;
using VideoHandler;

namespace RGBDReconstruction.Strategies;

public class MultiViewVideoProcessor : MultiViewProcessor
{
    private List<VideoStreamHandler> _videoStreamHandlersDepth;
    private List<VideoStreamHandler> _videoStreamHandlersRGB;

    private ConcurrentPriorityQueue<int, IntPtr>[] _videoFramesDepth;
    private ConcurrentPriorityQueue<int, IntPtr>[] _videoFramesRGB;

    public MultiViewVideoProcessor(string directoryPath) : base(directoryPath)
    { }

    public void PrepareVideoFiles()
    {
        for (int i = 0; i < _numCams; i++)
        {
            var fileName = GetVideoFileName(i + 1, ".mkv", "");
            var videoStreamHandler = new VideoStreamHandler();
            videoStreamHandler.Initialize(fileName);
            _videoStreamHandlersRGB.Add(videoStreamHandler);
            // TODO: Add depth
        }
    }

    public unsafe (IntPtr,IntPtr)[] GetFirstVideoFrame()
    {
        var res = new (IntPtr, IntPtr)[_numCams];
        for (int i = 0; i < _numCams; i++)
        {
            var data = new IntPtr(_videoStreamHandlersRGB[i].GetNextFrame()->data[0]);
            // TODO: Add depth
            res[i] = (data, data);
        }

        return res;
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
            frameData[i] = (rgbData.Value, depthData.Value);
        }

        return frameData;
    }

    private bool NextFrameDataPreparedForAllCameras()
    {
        var isReady = true;

        for (int i = 0; i < _videoStreamHandlersRGB.Count; i++)
        {
            var _RGBFrameData = _videoFramesRGB[i];
            var _depthFrameData = _videoFramesDepth[i];
            
            // isReady = isReady && !((_videoFramesRGB[i].Count < 20 || _videoFramesDepth[i].Count < 20) &&
            //                        _nextFrameToReturn == 1);

            isReady = isReady && !(!_videoFramesRGB[i].TryPeek(out var _) || !_videoFramesDepth[i].TryPeek(out var _));
        
            // _RGBFrameData.TryPeek(out var rgb);
            // isReady = isReady && rgb.Key == _nextFrameToReturn;
            //
            // _depthFrameData.TryPeek(out var depth);
            // isReady = isReady && (depth.Key == _nextFrameToReturn);
        }

        return isReady;
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
                    // var data = new IntPtr(_videoStreamHandlers[i].GetNextFrame()->data[0]);
                    // if (data == IntPtr.Zero)
                    // {
                    //     hasNextFrame = false;
                    //     break;
                    // }
                    // _videoFrames[i].Enqueue(frameIdx,data);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("All depth video frames loaded or error: " + e);
        }
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
    
    private String GetVideoFileName(int cam, String extension, String path)
    {
        var numCamDigits = (int)Math.Floor(Math.Log10(cam)) + 1;
        
        // max is 4 digits
        var camStr = "";
        
        for (int c = 0; c < (3 - numCamDigits); c++)
        {
            camStr += "0";
        }

        camStr += cam.ToString();

        return DirectoryPath + "\\cam_" + camStr + "\\rgb_cam_" + camStr + extension;
    }
}