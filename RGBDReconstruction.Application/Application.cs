using System.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common.Input;

using RGBDReconstruction.Strategies;

namespace RGBDReconstruction.Application;

public class Application(int width, int height, string title) : GameWindow(GameWindowSettings.Default,
    new NativeWindowSettings() { ClientSize = (width, height), Title = title, Flags = ContextFlags.ForwardCompatible })
{

    // private int[] _indexArray;
    //
    // private Vector3 _lightPos;
    //
    // private int _vertexBufferObject;
    // private int _vertexArrayObject;
    // private int _elementBufferObject;
    //
    // private int _lightVertexArrayObject;
    //
    // private int _width;
    // private int _height;
    //
    // private Camera _camera;
    //
    // private Shader _shader;
    // private Shader _lightingShader;
    //
    // private Matrix4 _model;
    // private Matrix4 _view;
    // private Matrix4 _projection;
    //
    // private Matrix4 _lightModel;
    //
    // private Vector2 _prevMousePos;
    // private float _sensitivity;
    // private bool firstMove = true;
    //
    // private Texture _diffuseMap;
    // private Texture _specularMap;

    private IReconstructionApplication app;

    protected override void OnLoad()
    {
        
        // Runs once when the window opens. Put initialization code here!
        base.OnLoad();
        
        CursorState = CursorState.Grabbed;
        Cursor = MouseCursor.Empty;

        app = new BVHReconstruction();
        
        app.Init(Size.X, Size.Y);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        app.RenderFrame(args);
        
        Context.SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        app.Resize(e);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        app.UpdateFrame(IsFocused, MousePosition, KeyboardState, args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
      
        app.MouseWheel(e);
    }
}