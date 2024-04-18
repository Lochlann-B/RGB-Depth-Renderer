using Emgu.CV;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace RGBDReconstruction.Application;

public class RaycastReconstruction : IReconstructionApplication
{
    private int[] _indexArray;

    private int _vertexBufferObject;
    private int _vertexArrayObject;

    private int _width;
    private int _height;

    private Camera _camera;

    private Shader _raycastShader;
    
    private Matrix4 _view;
    private Matrix4 _projectionInv;

    private Vector2 _prevMousePos;
    private float _sensitivity;
    private bool firstMove = true;

    private Vector2 _screenSize = new();

    private Matrix3 _intrinsicMatrix;

    private static readonly float[] _quadVerts =
    [
        -1f, -1f, 
        1f, 1f, 
        -1f, 1f, 
        -1f, -1f, 
        1f, -1f, 
        1f, 1f,
    ];
    
    // protected MultiViewProcessor _viewProcessor;
    // private float[,] _depthMap1;
    // private float[,] _depthMap2;

    // private int _depthBufferTexture;
    // private Texture _depthBufferTexture1;
    // private Texture _depthBufferTexture2;
    // private Texture _rgbTexture1;
    // private Texture _rgbTexture2;
    // private Matrix4 _depthMapCamPose1;
    // private Matrix4 _depthMapCamPose2;

    private int _frame = 1;

    // private double _elapsedTime = 0d;

    private MultiViewFramePreparer _framePreparer;

    private List<Matrix4> _depthCamPoses;

    public void Init(int windowWidth, int windowHeight)
    {
        _width = windowWidth;
        _height = windowHeight;
        _screenSize[0] = _width;
        _screenSize[1] = _height;
        
        GL.Enable(EnableCap.DepthTest);

        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        _camera = new Camera();
        _sensitivity = 0.1f;
        
        _vertexBufferObject = GL.GenBuffer();
        
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        
        GL.BufferData(BufferTarget.ArrayBuffer, _quadVerts.Length * sizeof(float), _quadVerts, BufferUsageHint.StaticDraw);
        
        // VAO - Vertex Array Object - specifies how the vertex attribute information is stored, formatted, and which buffers the data comes from
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        _raycastShader = new Shader("./shaders/Raycaster.vert", "./shaders/Raycaster.frag");
        _raycastShader.Use();

        var posLoc = _raycastShader.GetAttribLocation("vertexPos");
        
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(posLoc);
        
        _view = Matrix4.CreateTranslation(0.0f, 0.0f, -0.0f);
        _projectionInv = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), windowWidth / (float)windowHeight, 0.1f, 100.0f).Inverted();
        _raycastShader.SetUniformMatrix4f("viewMatrix", ref _view);
        _raycastShader.SetUniformMatrix4f("inverseProjectionMatrix", ref _projectionInv);

        var focalLength = 50f;
        var sensorWidth = 36f;
        var width = 1920f;
        var height = 1080f;
        var cx = width / 2f;
        var cy = height / 2f;

        var fx = width * (focalLength / sensorWidth);
        var fy = fx;

        var K = new Matrix3(new Vector3(fx, 0, cx), new Vector3(0, fy, cy), new Vector3(0, 0, 1));
        _intrinsicMatrix = K;
        //_intrinsicMatrix.Transpose();
        _raycastShader.SetUniformMatrix3f("intrinsicMatrix", ref _intrinsicMatrix);

        _framePreparer = new MultiViewFramePreparer(0);
        _framePreparer.Init();

        _depthCamPoses = _framePreparer.DepthCamPoses;

        // _viewProcessor = new MultiViewProcessor(@"C:\Users\Locky\Desktop\renders\chain_collision");
        // Task.Run(() => _viewProcessor.LoadFramesRGBAsync());
        // Task.Run(() => _viewProcessor.LoadFramesDepthAsync());

        // _depthCamPoses = _viewProcessor.GetCameraPoseInformation();
        // var camPose = _depthCamPoses[0];
        // _depthMapCamPose1 = camPose;
        // _depthMapCamPose1.Transpose();
        // _raycastShader.SetUniformMatrix4f("depthMapCamPoses[0]", ref _depthMapCamPose1);
        //
        // var camPose2 = _depthCamPoses[1];
        // _depthMapCamPose2 = camPose2;
        // _depthMapCamPose2.Transpose();
        // _raycastShader.SetUniformMatrix4f("depthMapCamPoses[1]", ref _depthMapCamPose2);
        //
        // _depthMap1 = _viewProcessor.GetDepthMap(1, 1);
        // _depthBufferTexture1 = new Texture(_depthMap1);
        // _depthBufferTexture1.Use(TextureUnit.Texture0);
        //
        // _depthMap2 = _viewProcessor.GetDepthMap(1, 2);
        // _depthBufferTexture2 = new Texture(_depthMap2);
        // _depthBufferTexture2.Use(TextureUnit.Texture0 + 1);
        //
        // _rgbTexture1 = new Texture("C:\\Users\\Locky\\Desktop\\renders\\chain_collision\\rgb\\frame_0001_cam_001.png");
        // _rgbTexture1.Use(TextureUnit.Texture0 + 1);
        //
        // _rgbTexture2 = new Texture("C:\\Users\\Locky\\Desktop\\renders\\chain_collision\\rgb\\frame_0001_cam_002.png");
        // _rgbTexture2.Use(TextureUnit.Texture0 + 2 + 1);



        // _depthBufferTexture = GL.GenTexture();
        // GL.BindTexture(TextureTarget.Texture2D, _depthBufferTexture);
        // GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, _depthMap.GetLength(1),
        //     _depthMap.GetLength(0));
        // GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _depthMap.GetLength(1), _depthMap.GetLength(0),
        //     PixelFormat.Red, PixelType.Float, _depthMap);
        //
        // GL.ActiveTexture(TextureUnit.Texture0);
        // //GL.BindImageTexture(0, _depthBufferTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f); 
        // GL.BindTexture(TextureTarget.Texture2D, _depthBufferTexture);

    }
    
    private Vector4 raycast(Vector3 worldRayStart, Vector3 worldRayDirection, Matrix4 depthMapCamPose, Matrix3 intrinsicMatrix, float[,] depthMap) {
        Vector4 rayO = depthMapCamPose * new Vector4(worldRayStart, 1.0f);
        Vector4 rayD = Vector4.Normalize(depthMapCamPose * new Vector4(worldRayDirection, 0f));

        float s = 0;
        Vector4 p = rayO + s*rayD;
        Vector3 imageCoords = intrinsicMatrix * (p).Xyz;

        var zOffset = imageCoords.Z == 0 ? 0.01f : 0f;

        Vector2 coords = new Vector2(imageCoords.X/(imageCoords.Z + zOffset), imageCoords.Y/(imageCoords.Z + zOffset));

        if (coords.X >= 1920 || coords.X >= 1080) {
            return new Vector4(0, 0, 0, 0);
        }

        float depth = depthMap[(int)coords.Y, (int)coords.X];

        int maxIters = 100;

        s = p.Z - depth;
        for (int i = 0; i < maxIters; i++) {
            if (-s > 1e-4) {
                s = float.Clamp(s, -0.4f, 0.4f);
                p = p + rayD*(0.8f*s);

                imageCoords = intrinsicMatrix * (p).Xyz;

                coords = new Vector2(imageCoords.X/imageCoords.Z, imageCoords.Y/imageCoords.Z);

                if (coords.X >= 1920 || coords.Y >= 1080) {
                    return new Vector4(0, 0, 0, 0);
                }

                depth = depthMap[(int)coords.Y, (int)coords.X];
            
                s = p.Z - depth;
            } else {
                return new Vector4(p.Xyz, 1.0f);
            }
        }
        return new Vector4(0,0,0,0);
    }

    public void RenderFrame(FrameEventArgs args)
    {
        var nonInvView = _camera.LookAt;//.Inverted();
        nonInvView = Matrix4.CreateRotationZ((float)Math.PI) * nonInvView;
        nonInvView.Transpose();
        _view = nonInvView.Inverted();
        _projectionInv = _camera.CameraProjectionMatrix.Inverted();
        
        //blep

        // var coordList = new List<Vector2>();
        // var count = 0;
        // for (var x = 0; x < _screenSize.X; x++)
        // {
        //     for (var y = 0; y < _screenSize.Y; y++)
        //     {
        //         var screenSize = _screenSize;
        //         var ndc = (new Vector2(x,y) / screenSize);
        //         ndc *= 2;
        //         ndc -= new Vector2(1f, 1f);
        //         //ndc.y = 1.0 - ndc.y;
        //
        //         // Unproject to camera space
        //         var rayClip = new Vector4(ndc[0], ndc[1], -1.0f, 1.0f);
        //         var rayCamera = _projectionInv * rayClip;
        //         rayCamera[2] = -1.0f; 
        //         rayCamera[3] = 0.0f;   
        //
        //         var rayWorld = Vector3.Normalize(((_view) * rayCamera).Xyz);
        //         var cameraPosition = (_view * new Vector4(0,0,0,1)).Xyz;
        //
        //         var blep = raycast(cameraPosition, rayWorld, _depthMapCamPose, _intrinsicMatrix, _depthMap);
        //
        //         if (blep.W > 0)
        //         {
        //             count++;
        //             coordList.Add(new Vector2(x, y));
        //             //_depthMap[y, x] = 1.0f;
        //         }
        //         else
        //         {
        //             //_depthMap[y, x] = 0f;
        //         }
        //     }
        // }
        
        
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        GL.BindVertexArray(_vertexArrayObject);
        
        // GL.BindTexture(TextureTarget.Texture2D, _depthBufferTexture);
        // GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, _depthMap.GetLength(1),
        //     _depthMap.GetLength(0));
        // GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _depthMap.GetLength(1), _depthMap.GetLength(0),
        //     PixelFormat.Red, PixelType.Float, _depthMap);
        
        //GL.BindImageTexture(0, _depthBufferTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);

        // _rgbTexture.Use(TextureUnit.Texture1);
        // _depthBufferTexture.Use();
        
        // _depthMap = _viewProcessor.GetDepthMap(_frame, 1);
        // _depthBufferTexture = new Texture(_depthMap);
        // _rgbTexture = new Texture(_viewProcessor.GetPNGFileName(_frame, 1));

        // _elapsedTime += args.Time;
        // if (_elapsedTime >= 1 / 60d)
        // {
        //     var nextFrameData = _viewProcessor.GetNextAvailableFrame();
        //     if (nextFrameData is not null)
        //     {
        //         _depthMapCamPose2 = _depthCamPoses[1];
        //         _depthMapCamPose2.Transpose();
        //         var fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        //         GL.WaitSync(fence, WaitSyncFlags.None, -1);
        //         GL.DeleteSync(fence);
        //         
        //         var rgbData = nextFrameData.Value.Item1;
        //         var depthData = nextFrameData.Value.Item2;
        //         _rgbTexture2.UpdateWithByteData(rgbData);
        //         _depthBufferTexture2.UpdateWithFloatArrayData(depthData);
        //         
        //         var fence2 = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        //         GL.WaitSync(fence2, WaitSyncFlags.None, -1);
        //         GL.DeleteSync(fence2);
        //     }
        //
        //     _elapsedTime = 0d;
        // }
        
        _framePreparer.TryUpdateNextFrames(args.Time);
        
        //_rgbTexture.UpdateTexture(_viewProcessor.GetPNGFileName(_frame, 1));
        
        var depthMapsArr = new[] { 0, 1,};
        _raycastShader.SetUniformInts("depthMaps", ref depthMapsArr);
        _framePreparer.UseDepthMapTextures(depthMapsArr);
        // _depthBufferTexture1.Use(TextureUnit.Texture0);
        // _depthBufferTexture2.Use(TextureUnit.Texture0 + 1);
        
  
        var rgbMapsArr = new[] { 2, 3};
        _raycastShader.SetUniformInts("rgbMaps", ref rgbMapsArr);
        _framePreparer.UseRGBMapTextures(rgbMapsArr);
        // _rgbTexture1.SetLocation(rgbLoc);
        // _rgbTexture1.Use(TextureUnit.Texture0 + 2);
        // _rgbTexture2.Use(TextureUnit.Texture0 + 2 + 1);
        //_rgbTexture.Use(TextureUnit.Texture1);
        
        _raycastShader.Use();
        _raycastShader.SetUniformMatrix4f("viewMatrix", ref _view);
        _raycastShader.SetUniformMatrix4f("inverseProjectionMatrix", ref _projectionInv);
        _raycastShader.SetUniformVec2("screenSize", ref _screenSize);
        _raycastShader.SetUniformMatrix3f("intrinsicMatrix", ref _intrinsicMatrix);
        for (int i = 0; i < _depthCamPoses.Count; i++)
        {
            var pose = _depthCamPoses[i];
            _raycastShader.SetUniformMatrix4f($"depthMapCamPoses[{i}]", ref pose);
        }
        // _raycastShader.SetUniformMatrix4f("depthMapCamPoses[0]", ref _depthMapCamPose1);
        // _raycastShader.SetUniformMatrix4f("depthMapCamPoses[1]", ref _depthMapCamPose2);
        // _raycastShader.SetUniformInt("depthMap", 0);
        // _raycastShader.SetUniformInt("rgbMap", 1);
        
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _frame++;
    }

    public void UpdateTextures()
    {
        
    }

    public void Resize(ResizeEventArgs e)
    {
        _width = e.Width;
        _height = e.Height;
        
        _screenSize[0] = _width;
        _screenSize[1] = _height;

        _camera.AspectRatio = _width / (float)_height;
        
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    public void UpdateFrame(bool IsFocused, Vector2 MousePosition, KeyboardState keyboardState, FrameEventArgs args)
    {
        if (firstMove)
        {
            _prevMousePos = new Vector2(MousePosition.X, MousePosition.Y);
            firstMove = false;
        }
        if (IsFocused)
        {
            float deltaX = MousePosition.X - _prevMousePos.X;
            float deltaY = MousePosition.Y - _prevMousePos.Y;
            _prevMousePos.X = MousePosition.X;
            _prevMousePos.Y = MousePosition.Y;
            
            _camera.HandleInput(keyboardState, new Vector2(deltaX, deltaY), _sensitivity, args.Time);
        }
    }
    

    public void ReconstructScene(int sceneNo, int voxelGridSize)
    {
        
    }

    public void MouseWheel(MouseWheelEventArgs e)
    {
        _camera.Fov -= e.OffsetY*0.02f;
    }
}