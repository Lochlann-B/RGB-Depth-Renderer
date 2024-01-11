using System.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common.Input;

namespace RGBDReconstruction.Application;

public class Application(int width, int height, string title) : GameWindow(GameWindowSettings.Default,
    new NativeWindowSettings() { ClientSize = (width, height), Title = title, Flags = ContextFlags.ForwardCompatible })
{
    
    // TODO: Get rid of this tutorial stuff!
private readonly float[] _vertices =
        {
             // Position          Normal
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f, // Front face
             0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
             0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,

            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f, // Back face
             0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,

            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f, // Left face
            -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,
            -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,

             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f, // Right face
             0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,

            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f, // Bottom face
             0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,

            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f, // Top face
             0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
             0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
            -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,
            -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f
        };

    private Vector3 _lightPos;

    private int _vertexBufferObject;
    private int _vertexArrayObject;

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

    protected override void OnLoad()
    {
        // Runs once when the window opens. Put initialization code here!
        base.OnLoad();
        
        GL.Enable(EnableCap.DepthTest);

        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

        _camera = new Camera();
        _sensitivity = 0.1f;

        CursorState = CursorState.Grabbed;
        Cursor = MouseCursor.Empty;
        
        // You can bind several buffers at once as long as they are of different types.
        // VBO is an array buffer type object
        _vertexBufferObject = GL.GenBuffer();
        
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        // ^ Now any calls to arraybuffer target will actually be referring to the VBO
        
        // BufferData is what lets us put user-defined data into a buffer
        // Static - likely won't change that much
        // Dynamic - likely to change a lot
        // Stream - Will change every time it is drawn
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
        
        // VAO - Vertex Array Object - specifies how the vertex attribute information is stored, formatted, and which buffers the data comes from
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);
        
        // Element buffer needs to be bound *after* the VAO has been bound
        
        _lightingShader = new Shader("./shaders/shader.vert", "./shaders/lighting.frag");
        _lightingShader.Use();
        
        // The 'index' param below refers to the location in the shader program we are putting data into.
        // That is, the layout (location = 0) in shader.vert
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);

        var normalLoc = _lightingShader.GetAttribLocation("aNormal");
        GL.EnableVertexAttribArray(normalLoc);
        GL.VertexAttribPointer(normalLoc, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        
        
        // The VBO that the VAO takes its data from is determined by the VBO currently bound to the ArrayBuffer (i.e. that on line 43)

        var objectColor = new Vector3(0.5f, 0.5f, 0.0f);
        var lightColor = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("objectColor", ref objectColor);
        _lightingShader.SetUniformVec3("lightColor", ref lightColor);

        _model = Matrix4.Identity;
        _view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
        _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Size.X / (float)Size.Y, 0.1f, 100.0f);
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);
        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        // shader for the light source
        _lightVertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_lightVertexArrayObject);
        
        _shader = new Shader("./shaders/shader.vert", "./shaders/shader.frag");
        _shader.Use();

        var vertexLocation = _shader.GetAttribLocation("aPosition");
        GL.EnableVertexAttribArray(vertexLocation);
        GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);

        _lightPos = new Vector3(1.2f, 1.0f, 2.0f);

        _lightModel = Matrix4.Identity;
        _lightModel *= Matrix4.CreateScale(0.5f);
        _lightModel *= Matrix4.CreateTranslation(_lightPos);
        _shader.SetUniformMatrix4f("model", ref _lightModel);
        _shader.SetUniformMatrix4f("view", ref _view);
        _shader.SetUniformMatrix4f("projection", ref _projection);

    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        _view = _camera.LookAt;
        _projection = _camera.CameraProjectionMatrix;
        
        base.OnRenderFrame(args);
        
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        
        
        // Render frame code goes here - run every frame
        GL.BindVertexArray(_lightVertexArrayObject);
        _shader.Use();
        _shader.SetUniformMatrix4f("model", ref _lightModel);
        _shader.SetUniformMatrix4f("view", ref _view);
        _shader.SetUniformMatrix4f("projection", ref _projection);
        
        
        
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
        
        GL.BindVertexArray(_vertexArrayObject);
        _lightingShader.Use();
        _lightingShader.SetUniformMatrix4f("model", ref _model);
        _lightingShader.SetUniformMatrix4f("view", ref _view);
        _lightingShader.SetUniformMatrix4f("projection", ref _projection);

        var vLightPos = new Vector4(_lightPos, 1.0f);
        vLightPos *= _view;
        var v3LightPos = vLightPos.Xyz;
        _lightingShader.SetUniformVec3("lightPos", ref v3LightPos);

        var normalMat = new Matrix3(Matrix4.Transpose(Matrix4.Invert(Matrix4.Mult(_model, _view))));
        _lightingShader.SetUniformMatrix3f("normal", ref normalMat);
        
        var objectColor = new Vector3(1.0f, 0.5f, 0.31f);
        var lightColor = new Vector3(1.0f, 1.0f, 1.0f);
        _lightingShader.SetUniformVec3("objectColor", ref objectColor);
        _lightingShader.SetUniformVec3("lightColor", ref lightColor);
        
                
                
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
        
        Context.SwapBuffers();
        
        
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        _width = e.Width;
        _height = e.Height;

        _camera.AspectRatio = _width / (float)_height;
        
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

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
            
            _camera.HandleInput(KeyboardState, new Vector2(deltaX, deltaY), _sensitivity, args.Time);
        }

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
      
        _camera.Fov -= e.OffsetY*0.02f;
    }
}