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

    private IReconstructionApplication app;

    protected override void OnLoad()
    {
        
        // Runs once when the window opens. Put initialization code here!
        base.OnLoad();
        
        CursorState = CursorState.Grabbed;
        Cursor = MouseCursor.Empty;

        // app = new BVHReconstruction();
        app = new RaycastReconstruction();
        
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