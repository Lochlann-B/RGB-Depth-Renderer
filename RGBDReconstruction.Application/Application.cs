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
    private readonly float[] _vertices = {
        //Position          Texture coordinates
        0.5f,  0.5f, 0.0f, 1.0f, 1.0f, // top right
        0.5f, -0.5f, 0.0f, 1.0f, 0.0f, // bottom right
        -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, // bottom left
        -0.5f,  0.5f, 0.0f, 0.0f, 1.0f  // top left
    };
    private readonly uint[] _indices =
    {
        0, 1, 3,
        1, 2, 3
    };

    private int _vertexBufferObject;
    private int _elementBufferObject;
    private int _vertexArrayObject;

    private int _width;
    private int _height;

    private Camera _camera;

    private Shader _shader;
    private Texture _texture1;
    private Texture _texture2;

    private Matrix4 _model;
    private Matrix4 _view;
    private Matrix4 _projection;

    private Vector2 _prevMousePos;
    private float _sensitivity;
    private bool firstMove = true;

    protected override void OnLoad()
    {
        // Runs once when the window opens. Put initialization code here!
        base.OnLoad();

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
        _elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
        
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);
        
        _shader = new Shader("./shader.vert", "./shader.frag");
        _shader.Use();
        
        // The 'index' param below refers to the location in the shader program we are putting data into.
        // That is, the layout (location = 0) in shader.vert
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        

        int texCoordLocation = _shader.GetAttribLocation("aTexCoord");
        GL.EnableVertexAttribArray(texCoordLocation);
        GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        
        
        // The VBO that the VAO takes its data from is determined by the VBO currently bound to the ArrayBuffer (i.e. that on line 43)

        _texture1 = new Texture("./resources/container.jpg");
        _texture1.Use(TextureUnit.Texture0);

        _texture2 = new Texture("./resources/awesomeface.png");
        _texture2.Use(TextureUnit.Texture1);
        
        _shader.SetUniformInt("texture1", 0);
        _shader.SetUniformInt("texture2", 1);

        _model = Matrix4.CreateRotationZ(Single.Pi / 2);
        _view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
        _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), Size.X / (float)Size.Y, 0.1f, 100.0f);
        _shader.SetUniformMatrix4f("model", ref _model);
        _shader.SetUniformMatrix4f("view", ref _view);
        _shader.SetUniformMatrix4f("projection", ref _projection);
        
        
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        _view = _camera.LookAt;
        _projection = _camera.CameraProjectionMatrix;
        
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // Render frame code goes here - run every frame
        GL.BindVertexArray(_vertexArrayObject);
        _shader.SetUniformMatrix4f("model", ref _model);
        _shader.SetUniformMatrix4f("view", ref _view);
        _shader.SetUniformMatrix4f("projection", ref _projection);
        
        _texture1.Use(TextureUnit.Texture0);
        _texture2.Use(TextureUnit.Texture1);
        _shader.Use();
        
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
        
        Context.SwapBuffers();
        
        base.OnRenderFrame(args);
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