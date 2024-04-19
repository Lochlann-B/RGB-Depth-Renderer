using System.Diagnostics;
using ILGPU.IR.Analyses;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using TextureUnit = OpenTK.Graphics.OpenGL4.TextureUnit;

namespace RGBDReconstruction.Application;

public class MultiViewFramePreparer
{
    private MultiViewProcessor _viewProcessor;

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
                _viewProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\chain_collision");
                break;
        }
    }

    public void Init()
    {
        DepthCamPoses = _viewProcessor.GetCameraPoseInformation();
        for (int i = 0; i < DepthCamPoses.Count; i++)
        {
            var mat = DepthCamPoses[i];
            mat.Transpose();
            DepthCamPoses[i] = mat;
        }

        _rgbTextures = new List<Texture>();
        _depthTextures = new List<Texture>();
        
        var rgbdepth = _viewProcessor.GetFirstFrame();
        
        Task.Run(() => _viewProcessor.LoadFramesRGBAllCams());
        Task.Run(() => _viewProcessor.LoadFramesDepthAllCams());

       

        for (int i = 0; i < rgbdepth.Count; i++)
        {
            var rgbtex = new Texture(rgbdepth[i].Item1, 1920, 1080);
           var depthtex = new Texture(rgbdepth[i].Item2);
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
        // _depthTextures
    }

    public void TryUpdateNextFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime * _incTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return;
        }

        var nextFrameData = _viewProcessor.GetNextAvailableFrame();
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
}