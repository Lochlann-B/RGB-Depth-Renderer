using System.Diagnostics;
using ILGPU.IR.Analyses;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RGBDReconstruction.Strategies;
using TextureUnit = OpenTK.Graphics.OpenGL4.TextureUnit;

namespace RGBDReconstruction.Application;

public class MultiViewFramePreparer
{
    private MultiViewProcessor _viewImgProcessor;
    private MultiViewVideoProcessor _viewVidProcessor;

    private List<Texture> _rgbTextures;
    private List<Texture> _depthTextures;

    public List<Matrix4> DepthCamPoses { get; private set; }

    private double _elapsedTimeSinceLastFrame = 0d;
    private int _incTime = 1;
    
    public MultiViewFramePreparer(int sceneNo)
    {
        switch (sceneNo)
        {
            case 0:
                _viewImgProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\chain_collision");
                _viewVidProcessor = new MultiViewVideoProcessor(@"C:\Users\Locky\Desktop\renders\chain_collision");
                break;
        }
    }

    public void InitImgTextures()
    {
        DepthCamPoses = _viewImgProcessor.GetCameraPoseInformation();
        for (int i = 0; i < DepthCamPoses.Count; i++)
        {
            var mat = DepthCamPoses[i];
            mat.Transpose();
            DepthCamPoses[i] = mat;
        }

        _rgbTextures = new List<Texture>();
        _depthTextures = new List<Texture>();
        
        var rgbdepth = _viewImgProcessor.GetFirstFrame();
        
        Task.Run(() => _viewImgProcessor.LoadFramesRGBAllCams());
        Task.Run(() => _viewImgProcessor.LoadFramesDepthAllCams());

       

        for (int i = 0; i < rgbdepth.Count; i++)
        {
            var rgbtex = new Texture(rgbdepth[i].Item1, 1920, 1080);
           var depthtex = new Texture(rgbdepth[i].Item2);
            _depthTextures.Add(depthtex);
            _rgbTextures.Add(rgbtex);
            
        }
    }

    public void InitVideoTextures()
    {
        DepthCamPoses = _viewVidProcessor.GetCameraPoseInformation();
        for (int i = 0; i < DepthCamPoses.Count; i++)
        {
            var mat = DepthCamPoses[i];
            mat.Transpose();
            DepthCamPoses[i] = mat;
        }

        _rgbTextures = new List<Texture>();
        _depthTextures = new List<Texture>();
        
        var rgbdepth = _viewVidProcessor.GetFirstVideoFrame();
        
        Task.Run(() => _viewVidProcessor.LoadFramesRGBAllCams());
        Task.Run(() => _viewVidProcessor.LoadFramesDepthAllCams());

       

        for (int i = 0; i < rgbdepth.Length; i++)
        {
            var rgbtex = new Texture(rgbdepth[i].Item1, 1920, 1080);
            var depthtex = new Texture(rgbdepth[i].Item2, 1920, 1080);
            _depthTextures.Add(depthtex);
            _rgbTextures.Add(rgbtex);
        }
    }

    public void UseDepthMapTextures(int[] arr)
    {
        foreach (var i in arr)
        {
            _depthTextures[i -  _depthTextures.Count].Use(TextureUnit.Texture0 + i);
        }
        // _depthTextures
    }
    
    public void UseRGBMapTextures(int[] arr)
    {
        foreach (var i in arr)
        {
            _rgbTextures[i].Use(TextureUnit.Texture0 + i);
        }
    }
    
    public void TryUpdateNextVideoFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime * _incTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return;
        }

        var nextFrameData = _viewVidProcessor.GetNextAvailableVideoFrame();
        if (nextFrameData is null)
        {
            return;
        }

        _incTime = 0;
  
        UpdateFrames(nextFrameData);

        _elapsedTimeSinceLastFrame = 0d;
        _incTime = 1;
    }

    public void TryUpdateNextFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime * _incTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return;
        }

        var nextFrameData = _viewImgProcessor.GetNextAvailableFrame();
        if (nextFrameData is null)
        {
            return;
        }

        _incTime = 0;
        // var fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        // GL.WaitSync(fence, WaitSyncFlags.None, -1);
        // GL.DeleteSync(fence);
        // var watch = Stopwatch.StartNew();
        // Task.Run((() => UpdateFrames(nextFrameData))).ContinueWith(t => {_elapsedTimeSinceLastFrame = 0d;
        // _incTime = 1;
        // }, TaskContinuationOptions.OnlyOnRanToCompletion);
        UpdateFrames(nextFrameData);

        _elapsedTimeSinceLastFrame = 0d;
        _incTime = 1;

        // for (int i = 0; i < nextFrameData.Length-5; i++)
        // {
        //     var rgb = nextFrameData[i].Item1;
        //     var depth = nextFrameData[i].Item2;
        //     
        //     _rgbTextures[i].UpdateWithByteData(rgb);
        //     //_depthTextures[i].UpdateWithFloatArrayData(depth);
        // }
        // watch.Stop();
        // Console.WriteLine(watch.ElapsedMilliseconds);
        // var fence2 = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        // GL.WaitSync(fence2, WaitSyncFlags.None, -1);
        // GL.DeleteSync(fence2);
        // _elapsedTimeSinceLastFrame = 0d;
    }

    private void UpdateFrames((byte[], float[,])[] nextFrameData)
    {
        for (int i = 0; i < nextFrameData.Length; i++)
        {
            var rgb = nextFrameData[i].Item1;
            var depth = nextFrameData[i].Item2;
            
            _rgbTextures[i].UpdateWithByteData(rgb);
            _depthTextures[i].UpdateWithFloatArrayData(depth);
        }
    }

    private void UpdateFrames((IntPtr, IntPtr)[] nextFrameData)
    {
        for (int i = 0; i < nextFrameData.Length; i++)
        {
            var rgb = nextFrameData[i].Item1;
            var depth = nextFrameData[i].Item2;
            
            _rgbTextures[i].UpdateWithPointer(rgb);
            _depthTextures[i].UpdateWithPointer(depth);
        }
    }
}