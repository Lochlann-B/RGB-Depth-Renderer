using System.Diagnostics;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
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
            case 1:
                _viewImgProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\forest");
                _viewVidProcessor = new MultiViewVideoProcessor(@"C:\Users\Locky\Desktop\renders\forest");
                break;
            case 2:
                _viewImgProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\skibidi toilet");
                _viewVidProcessor = new MultiViewVideoProcessor(@"C:\Users\Locky\Desktop\renders\skibidi toilet");
                break;
            case 3:
                _viewImgProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\palace");
                _viewVidProcessor = new MultiViewVideoProcessor(@"C:\Users\Locky\Desktop\renders\palace");
                break;
            case 4:
                _viewImgProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\detective desk");
                _viewVidProcessor = new MultiViewVideoProcessor(@"C:\Users\Locky\Desktop\renders\detective desk");
                break;
        }
    }

    public void InitImgTextures()
    {
        DepthCamPoses = _viewImgProcessor.GetCameraPoseInformation(false);
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
        DepthCamPoses = _viewVidProcessor.GetCameraPoseInformation(false);
        for (int i = 0; i < DepthCamPoses.Count; i++)
        {
            var mat = DepthCamPoses[i];
            mat.Transpose();
            DepthCamPoses[i] = mat;
        }
        
        _viewVidProcessor.PrepareVideoFiles();

        _rgbTextures = new List<Texture>();
        _depthTextures = new List<Texture>();

        var rgbdepth = _viewVidProcessor.AwaitNextFrame();

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
    }
    
    public void UseRGBMapTextures(int[] arr)
    {
        foreach (var i in arr)
        {
            _rgbTextures[i].Use(TextureUnit.Texture0 + i);
        }
    }
    
    public bool TryUpdateNextVideoFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime * _incTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return false;
        }

        var nextFrameData = _viewVidProcessor.GetNextAvailableVideoFrame();
        if (nextFrameData is null)
        {
            return false;
        }

        // _incTime = 0;
  
        UpdateFrames(nextFrameData);

        _elapsedTimeSinceLastFrame = 0d;
        _incTime = 1;

        return true;
    }

    public void TryUpdateNextFrames(double elapsedTime)
    {
        _elapsedTimeSinceLastFrame += elapsedTime * _incTime;

        if (_elapsedTimeSinceLastFrame < 1 / 60d)
        {
            return;
        }

        var nextFrameData = _viewVidProcessor.GetNextAvailableFrame();
        if (nextFrameData is null)
        {
            return;
        }

        _incTime = 0;

        UpdateFrames(nextFrameData);

        _elapsedTimeSinceLastFrame = 0d;
        _incTime = 1;
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
            
            _rgbTextures[i].UpdateWithPointer(rgb, false);
            _depthTextures[i].UpdateWithPointer(depth, false);
        }
    }
}