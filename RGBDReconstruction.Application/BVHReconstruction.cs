using System.Drawing;
using System.Drawing.Imaging;
using Emgu.CV.Ocl;
using Microsoft.VisualBasic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RGBDReconstruction.Strategies;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace RGBDReconstruction.Application;

public class BVHReconstruction : IReconstructionApplication
{
    private int[] _indexArray;

    private Vector3 _lightPos;

    private int _vertexBufferObject;
    private int _vertexArrayObject;
    private int _elementBufferObject;

    private int _lightVertexArrayObject;

    private int _width;
    private int _height;

    private Camera _camera;

    private Shader _shader;
    private Shader _lightingShader;

    private Matrix4 _model;
    private Matrix4 _view;
    private Matrix4 _projection;

    private Matrix4 _lightModel;

    private Vector2 _prevMousePos;
    private float _sensitivity;
    private bool firstMove = true;

    private Texture _diffuseMap;
    private Texture _specularMap;
    
    private List<int> _keyFrames = new List<int>();
    private List<Vector3> _positionsAtKeyFrames = new List<Vector3>();
    private List<Vector3> _rotationsAtKeyFrames = new List<Vector3>();

    private List<Texture> _textures;
    
    private int _vidFrame = 180;
    
    private string _evalPath = "C:\\Users\\2337G\\Desktop\\renders\\chain_collision\\evaluation data\\virtualcam_samepos_asdatacam\\cam1\\";
    private string _keyFrameText = "C:\\Users\\2337G\\Desktop\\renders\\chain_collision\\evaluation data\\testcamkeyframes.txt";
    
    private Matrix4 _animPose;
    
    public void Init(int windowWidth, int windowHeight)
    {
        _width = windowWidth;
        _height = windowHeight;
        
        GL.Enable(EnableCap.DepthTest);

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

        _camera = new Camera();
        _sensitivity = 0.1f;
        
        _vertexBufferObject = GL.GenBuffer();
        
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        
        GetTestCamAnimationData(_keyFrameText);
        
        UpdateAnimation();

        var multiViewReconstructor = new MultiViewVoxelGridReconstruction(0, 500);
        var mesh = multiViewReconstructor.GetFrameGeometry(180);
        var contiguousMeshData = mesh.GetContiguousMeshData();
        
        GL.BufferData(BufferTarget.ArrayBuffer, contiguousMeshData.Length * sizeof(float), contiguousMeshData, BufferUsageHint.StaticDraw);
        
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        // Element buffer needs to be bound *after* the VAO has been bound
        
        _lightingShader = new Shader("./shaders/shader.vert", "./shaders/lighting.frag");
        _lightingShader.Use();
        
        // The 'index' param below refers to the location in the shader program we are putting data into.
        // That is, the layout (location = 0) in shader.vert
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, mesh.MeshLayout.Stride * sizeof(float), 0);

        var normalLoc = _lightingShader.GetAttribLocation("aNormal");
        GL.EnableVertexAttribArray(normalLoc);
        GL.VertexAttribPointer(normalLoc, 3, VertexAttribPointerType.Float, false, mesh.MeshLayout.Stride * sizeof(float), 3 * sizeof(float));

        var colourLoc = _lightingShader.GetAttribLocation("aColour");
        GL.EnableVertexAttribArray(colourLoc);
        GL.VertexAttribPointer(colourLoc, 4, VertexAttribPointerType.Float, false, mesh.MeshLayout.Stride * sizeof(float), 6 * sizeof(float));
        
        _elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
        _indexArray = mesh.MeshLayout.IndexArray;
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.MeshLayout.IndexArray.Length * sizeof(uint), mesh.MeshLayout.IndexArray, BufferUsageHint.StaticDraw);
        
        // The VBO that the VAO takes its data from is determined by the VBO currently bound to the ArrayBuffer (i.e. that on line 43)

        var objectColor = new Vector3(0.5f, 0.5f, 0.0f);
        var lightColor = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("objectColor", ref objectColor);
        _lightingShader.SetUniformVec3("lightColor", ref lightColor);

        _model = Matrix4.Identity;
        _model[0, 0] = 1f;
        _model[1, 1] = 1f;
        _model[2, 2] = -1f;
        _view = Matrix4.CreateTranslation(0.0f, 0.0f, -0.0f);
        _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(39.6f*9/160f), windowWidth / (float)windowHeight, 0.1f, 100.0f);
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);
        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        _lightPos = new Vector3(-0.0f, -0.0f, 0.0f);
    }

    public void RenderFrame(FrameEventArgs args)
    {
        _view = _camera.LookAt;
        _view[3, 0] *= -1;
        _view[3, 1] *= -1;
        _view[3, 2] *= -1;
        _projection = _camera.CameraProjectionMatrix;

        
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        GL.BindVertexArray(_vertexArrayObject);
        
        _lightingShader.Use();
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);

        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        var vLightPos = new Vector4(_lightPos, 0.0f);
        vLightPos *= _view;
        var v3LightPos = vLightPos.Xyz;

        var normalMat = new Matrix3(Matrix4.Transpose(Matrix4.Invert(Matrix4.Mult(_model, _view))));
        _lightingShader.SetUniformMatrix3f("normal", ref normalMat);
        
        var objAmbient = new Vector3(1.0f, 0.5f, 0.31f);
        var objDiffuse = new Vector3(1.0f, 0.5f, 0.31f);
        var objSpecular = new Vector3(0.5f, 0.5f, 0.5f);
        var objShininess = 32;

        var lightAmbient = new Vector3(0.2f, 0.2f, 0.2f);
        var lightDiffuse = new Vector3(0.5f, 0.5f, 0.5f);
        var lightSpecular = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("material.specular", ref objSpecular);
        _lightingShader.SetUniformFloat("material.shininess", objShininess);
        
        _lightingShader.SetUniformVec3("light.ambient", ref lightAmbient);
        _lightingShader.SetUniformVec3("light.diffuse", ref lightDiffuse);
        _lightingShader.SetUniformVec3("light.specular", ref lightSpecular);
        _lightingShader.SetUniformVec3("light.direction", ref v3LightPos);
        
        
        _lightingShader.SetUniformInt("material.diffuse", 0);
        _lightingShader.SetUniformInt("material.specular", 1);
        
        GL.DrawElements(PrimitiveType.Triangles, _indexArray.Length, DrawElementsType.UnsignedInt, 0);
    }
    
    public void UpdateAnimation()
    {
        

        for (int i = 0; i < _keyFrames.Count-1; i++)
        {
            if (_keyFrames[i] <= _vidFrame && _keyFrames[i + 1] > _vidFrame)
            {
                var LBPos = _positionsAtKeyFrames[i];
                var UBPos = _positionsAtKeyFrames[i + 1];

                var LBRot = _rotationsAtKeyFrames[i];
                var UBRot = _rotationsAtKeyFrames[i + 1];

                var LBFrame = _keyFrames[i];
                var UBFrame = _keyFrames[i + 1];

                var interpVal = (_vidFrame - LBFrame) / (float)(UBFrame - LBFrame);

                var interpPos = LBPos + interpVal * (UBPos - LBPos);
                var interpRot = LBRot + interpVal * (UBRot - LBRot);


                interpPos.X *= -1;
                interpPos.Z *= -1;
                
                var T = Matrix4.CreateTranslation(interpPos.Xzy);
                var Rx = Matrix4.CreateRotationX((float)Math.PI*(interpRot.X-90f)/180f);
                var Ry = Matrix4.CreateRotationY((float)Math.PI*(interpRot.Z)/180f);
                var Rz = Matrix4.CreateRotationZ((float)Math.PI*interpRot.Y/180f);

                _animPose = Rx * Ry * Rz;

                _camera._position = interpPos.Xzy;
                _camera._roll = interpRot.Y;
                _camera._pitch = interpRot.X - 90f;
                _camera._yaw = 270f-interpRot.Z;
                
                _animPose = _animPose.Inverted();
                _animPose.Row3 = new Vector4(interpPos, 1.0f);
                // _animPose.Transpose();
                Console.WriteLine(_animPose);
                break;
            }
        }
    }
    
    private void GetTestCamAnimationData(string filepath)
    {
        var lines = File.ReadAllLines(filepath);
        

        var pos = new Vector3();
        var rot = new Vector3();
        for (int i = 0; i < lines.Length; i++)
        {
            
            if (i % 7 == 0 || i == 0)
            {
                var frame = int.TryParse(lines[i], out var frameNum);
                if (frame)
                {
                    _keyFrames.Add(frameNum);
                }

                if (i > 0)
                {
                    _positionsAtKeyFrames.Add(pos);
                    _rotationsAtKeyFrames.Add(rot);
                    pos = new Vector3();
                    rot = new Vector3();
                }
            }
            else if (i % 7 > 0 && i % 7 < 4)
            {
                var parsed = float.TryParse(lines[i], out var coord);
                if (parsed)
                    pos[(i % 7)-1] = coord;
            }
            else
            {
                var parsed = float.TryParse(lines[i], out var angle);
                if (parsed)
                    rot[(i % 7) - 4] = angle;
            }
        }
        _positionsAtKeyFrames.Add(pos);
        _rotationsAtKeyFrames.Add(rot);
    }
    
    private void CaptureScreenToFile(string filename)
    {
        int width = _width;
        int height = _height;

        // Create an array to hold the pixel data
        byte[] data = new byte[width * height * 4];  // 4 bytes for RGBA

        // Read the pixels from the framebuffer
        GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, data);
        // Swap R and B channels
        for (int i = 0; i < width * height * 4; i += 4)
        {
            (data[i], data[i + 2]) = (data[i + 2], data[i]);
        }
        // Use Bitmap to save the data as a PNG
        using (Bitmap bmp = new Bitmap(width, height))
        {
            // Lock the bitmap's bits
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Get the address of the first line
            System.Runtime.InteropServices.Marshal.Copy(data, 0, bmpData.Scan0, data.Length);

            // Unlock the bits
            bmp.UnlockBits(bmpData);

            // Save the bitmap as a PNG file
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY); // Optional: Flip the image vertically
            bmp.Save(filename, ImageFormat.Png);
        }
    }

    public void Resize(ResizeEventArgs e)
    {
        _width = e.Width;
        _height = e.Height;

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

        if (keyboardState.IsKeyPressed(Keys.T))
        {
            var filePath =
                _evalPath;
            filePath += "\\voxelgrid300\\vidFrame_" + _vidFrame + ".png";
            
            CaptureScreenToFile(filePath);
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